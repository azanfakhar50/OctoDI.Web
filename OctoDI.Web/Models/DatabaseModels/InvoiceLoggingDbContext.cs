using Microsoft.EntityFrameworkCore;

namespace OctoDI.Web.Models.DatabaseModels
{
    public class InvoiceLoggingDbContext : DbContext
    {
        public InvoiceLoggingDbContext(DbContextOptions<InvoiceLoggingDbContext> options)
            : base(options) { }

        public DbSet<InvoiceLog> InvoiceLogs { get; set; }
        public DbSet<InvoiceRequestLog> InvoiceRequestLogs { get; set; }
        public DbSet<InvoiceResponseLog> InvoiceResponseLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InvoiceLog>()
                .HasKey(i => i.InvoiceLogId);

            modelBuilder.Entity<InvoiceRequestLog>()
                .HasKey(r => r.InvoiceRequestLogId);

            modelBuilder.Entity<InvoiceResponseLog>()
                .HasKey(r => r.InvoiceResponseLogId);

            base.OnModelCreating(modelBuilder);
        }
    }
}
