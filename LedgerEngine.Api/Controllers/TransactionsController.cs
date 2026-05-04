using System;
using System.Linq;
using System.Threading.Tasks;
using LedgerEngine.Api.Data;
using LedgerEngine.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerEngine.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public TransactionsController(AppDbContext context)
        {
            _context = context;
        }
        public class TransactionRequest
        {
            public Guid FromAccountId { get; set; }
            public Guid ToAccountId { get; set; }
            public decimal Amount { get; set; }
            public Guid IdempotencyKey { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTransaction([FromBody] TransactionRequest request)
        {
            if (request.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            if (request.FromAccountId == request.ToAccountId)
                return BadRequest("Cannot transfer money to the same account.");

            bool alreadyExists = await _context.LedgerEntries
                .AnyAsync(e => e.IdempotencyKey == request.IdempotencyKey);

            if (alreadyExists)
                return Conflict("This transaction has already been processed.");

            var accountsExist = await _context.Accounts
                .Where(a => a.Id == request.FromAccountId || a.Id == request.ToAccountId)
                .CountAsync() == 2;

            if (!accountsExist)
                return NotFound("One or both accounts do not exist.");

            var transactionId = Guid.NewGuid();
            var timestamp = DateTime.UtcNow;

            var debitEntry = new LedgerEntry
            {
                AccountId = request.FromAccountId,
                TransactionId = transactionId,
                IdempotencyKey = request.IdempotencyKey,
                Amount = -request.Amount, 
                CreatedAt = timestamp
            };

            var creditEntry = new LedgerEntry
            {
                AccountId = request.ToAccountId,
                TransactionId = transactionId,
                IdempotencyKey = Guid.NewGuid(), 
                Amount = request.Amount,  
                CreatedAt = timestamp
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.LedgerEntries.Add(debitEntry);
                _context.LedgerEntries.Add(creditEntry);
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { Message = "Transaction successful", TransactionId = transactionId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "An error occurred while processing the transaction.");
            }
        }
    }
}