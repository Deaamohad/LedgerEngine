using System;

namespace LedgerEngine.Api.Models
{
    public class Account
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string OwnerName { get; set; } = string.Empty;
        public string Currency { get; set; } = "JOD";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}