using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace OctoDI.Web.Models.DatabaseModels
{
    public partial class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext()
        {
        }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Existing DbSets
        public virtual DbSet<Subscription> Subscriptions { get; set; }
        public virtual DbSet<User> Users { get; set; }

        // ✅ New DbSets for Invoice Module
        public virtual DbSet<Invoice> Invoices { get; set; }
        public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<SubscriptionSetting> SubscriptionSettings { get; set; }
        public DbSet<Buyer> Buyers { get; set; }
        public DbSet<FbrLog> FbrLogs { get; set; } = null!;
        public DbSet<Product> Products { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<ServiceCategory> ServiceCategories { get; set; }
        public DbSet<GlobalSaleRate> GlobalSaleRates { get; set; }
        public DbSet<TwoFactorOtp> TwoFactorOtps { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }





        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                var connectionString = config.GetConnectionString("DefaultConnection");
                optionsBuilder.UseSqlServer(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ====== Existing Configuration ======
            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.SubscriptionId).HasName("PK__Subscrip__9A2B249D8A930BFC");

                entity.HasIndex(e => e.NtnCnic, "UQ__Subscrip__04CA4E868583655F").IsUnique();

                entity.Property(e => e.Address).HasMaxLength(500);
                entity.Property(e => e.BlockReason).HasMaxLength(500);
                entity.Property(e => e.BusinessName).HasMaxLength(255);
                entity.Property(e => e.CompanyName).HasMaxLength(255);
                entity.Property(e => e.ContactEmail).HasMaxLength(255);
                entity.Property(e => e.ContactPerson).HasMaxLength(100);
                entity.Property(e => e.ContactPhone).HasMaxLength(20);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
                entity.Property(e => e.FbrSecurityToken).HasColumnName("FBR_SecurityToken");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.LogoUrl).HasMaxLength(500);
                entity.Property(e => e.MaxUsers).HasDefaultValue(10);
                entity.Property(e => e.NtnCnic)
                    .HasMaxLength(20)
                    .HasColumnName("NTN_CNIC");
                entity.Property(e => e.Province).HasMaxLength(100);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C0DEAED8F");

                entity.HasIndex(e => e.Username, "UQ__Users__536C85E4525369EE").IsUnique();
                entity.HasIndex(e => e.Email, "UQ__Users__A9D1053427C49F73").IsUnique();

                entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
                entity.Property(e => e.Email).HasMaxLength(255);
                entity.Property(e => e.FirstName).HasMaxLength(100);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.LastName).HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);
                entity.Property(e => e.ProfileImageUrl).HasMaxLength(500);
                entity.Property(e => e.UserRole).HasMaxLength(50);
                entity.Property(e => e.Username).HasMaxLength(100);

                entity.HasOne(d => d.Subscription).WithMany(p => p.Users)
                    .HasForeignKey(d => d.SubscriptionId)
                    .HasConstraintName("FK_Users_Subscriptions");
            });

            // ====== ✅ New Invoice Module Configuration ======

            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasKey(e => e.InvoiceId);
                entity.Property(e => e.InvoiceType).HasMaxLength(50);
                entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Draft");
                entity.Property(e => e.InvoiceRefNo).HasMaxLength(50);
                entity.Property(e => e.FBRInvoiceNo).HasMaxLength(50);
                entity.Property(e => e.CreatedBy).HasMaxLength(100);
                entity.Property(e => e.UpdatedBy).HasMaxLength(100);
                entity.Property(e => e.Remarks).HasMaxLength(250);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");

                entity.HasOne(d => d.Buyer)
                      .WithMany()
                      .HasForeignKey(d => d.BuyerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<InvoiceItem>(entity =>
            {
                entity.HasKey(e => e.ItemId);
                entity.Property(e => e.HSCode).HasMaxLength(20);
                entity.Property(e => e.ProductDescription).HasMaxLength(200);
                entity.Property(e => e.UOM).HasMaxLength(50);

                entity.HasOne(d => d.Invoice)
                      .WithMany(p => p.Items)
                      .HasForeignKey(d => d.InvoiceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Buyer>(entity =>
            {
                entity.HasKey(e => e.BuyerId);
                entity.Property(e => e.BuyerNTN).HasMaxLength(20);
                entity.Property(e => e.BuyerBusinessName).HasMaxLength(150);
                entity.Property(e => e.BuyerProvince).HasMaxLength(50);
                entity.Property(e => e.BuyerAddress).HasMaxLength(250);
                entity.Property(e => e.BuyerRegistrationType).HasMaxLength(50);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
                entity.Property(e => e.CreatedBy).HasMaxLength(100);

                entity.HasOne(d => d.Subscription)
                      .WithMany(p => p.Buyers)
                      .HasForeignKey(d => d.SubscriptionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<TwoFactorOtp>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.User)
                      .WithMany()          // if User has no collection of OTPs, else use Users.TwoFactorOtps
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable("TwoFactorOtp");
            });



        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
