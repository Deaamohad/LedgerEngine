using System;
using System.Threading.Tasks;
using LedgerEngine.Api.Data;
using LedgerEngine.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerEngine.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AccountsController(AppDbContext context)
        {
            _context = context;
        }

        public class CreateAccountRequest
        {
            public string OwnerName { get; set; } = string.Empty;
            public string Currency { get; set; } = "JOD";
        }

        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
        {
            var account = new Account
            {
                Id = Guid.NewGuid(),
                OwnerName = request.OwnerName,
                Currency = request.Currency,
                CreatedAt = DateTime.UtcNow
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            return Ok(account); 
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAccounts()
        {
            var accounts = await _context.Accounts.ToListAsync();
            return Ok(accounts);
        }
    }
}