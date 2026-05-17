using System;
using System.Linq;
using System.Threading.Tasks;
using LedgerEngine.Api.Data;
using LedgerEngine.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LedgerEngine.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly Guid _centralBankId;

        public TransactionsController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            var bankIdString = configuration["LedgerSettings:CentralBankId"];
            _centralBankId = Guid.Parse(bankIdString);
        }

        public class TransferRequest
        {
            public Guid ToAccountId { get; set; }
            public decimal Amount { get; set; }
            public Guid IdempotencyKey { get; set; }
        }

        [HttpPost("transfer")]
        public async Task<IActionResult> CreateTransaction([FromBody] TransferRequest request)
        {
            if (request.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid senderAccountId))
                return Unauthorized("Invalid token identity.");

            if (senderAccountId == request.ToAccountId)
                return BadRequest("Cannot transfer money to your own account.");

            bool alreadyExists = await _context.LedgerEntries
                .AnyAsync(e => e.IdempotencyKey == request.IdempotencyKey);
            
            if (alreadyExists)
                return Conflict("This transaction has already been processed.");

            var receiverExists = await _context.Accounts.AnyAsync(a => a.Id == request.ToAccountId);
            if (!receiverExists)
                return NotFound("The receiving account does not exist.");

            var senderBalance = await _context.LedgerEntries
                .Where(e => e.AccountId == senderAccountId)
                .SumAsync(e => e.Amount);

            var transactionId = Guid.NewGuid();
            var timestamp = DateTime.UtcNow;

            if (senderBalance < request.Amount)
                return BadRequest("Insufficient funds.");

            var debitEntry = new LedgerEntry
            {
                AccountId = senderAccountId,
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
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "An error occurred while processing the transaction.");
            }
        }

        [HttpGet("account/{accountId}/balance")]
        public async Task<IActionResult> GetAccountBalance(Guid accountId)
        {
            var accountExists = await _context.Accounts.AnyAsync(a => a.Id == accountId);
            if (!accountExists)
                return NotFound("Account not found.");

            var balance = await _context.LedgerEntries
                .Where(e => e.AccountId == accountId)
                .SumAsync(e => e.Amount);

            return Ok(new { 
                AccountId = accountId, 
                CurrentBalance = balance 
            });
        }

        [HttpGet("account/{accountId}/history")]
        public async Task<IActionResult> GetTransactionHistory(Guid accountId)
        {
            var history = await _context.LedgerEntries
                .Where(e => e.AccountId == accountId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            if (!history.Any())
                return NotFound("No transactions found for this account.");

            return Ok(history);
        }  

        [HttpPost("deposit")]
        public async Task<IActionResult> CreateDeposit([FromBody] DepositRequest request)
        {
            if (request.Amount <= 0)
            {
                return BadRequest("Deposit amount must be greater than zero.");
            }

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid accountId))
            {
                return Unauthorized("Invalid token identity.");
            }

            var newEntry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                TransactionId = Guid.NewGuid(),
                IdempotencyKey = Guid.NewGuid(), 
                Amount = request.Amount,
                CreatedAt = DateTime.UtcNow
            };

            _context.LedgerEntries.Add(newEntry);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Deposit successful", Entry = newEntry });
        }
    }

    public class DepositRequest
    {
        public decimal Amount { get; set; }
    }
}