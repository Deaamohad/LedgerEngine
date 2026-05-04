using LedgerEngine.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LedgerEngine.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<LedgerEntry> LedgerEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LedgerEntry>()
                .Property(l => l.Amount)
                .HasColumnType("decimal(18, 4)");
                
            modelBuilder.Entity<LedgerEntry>()
                .HasIndex(l => l.IdempotencyKey)
                .IsUnique();
        }
    }
}