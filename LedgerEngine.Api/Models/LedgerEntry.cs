using System;

namespace LedgerEngine.Api.Models
{
    public class LedgerEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AccountId { get; set; }
        public Guid TransactionId { get; set; }        
        public Guid IdempotencyKey { get; set; }
        public decimal Amount { get; set; }        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}