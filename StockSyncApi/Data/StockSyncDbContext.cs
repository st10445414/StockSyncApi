using Microsoft.EntityFrameworkCore;
using StockSyncApi.Models;

namespace StockSyncApi.Data;

public class StockSyncDbContext : DbContext
{
    public StockSyncDbContext(DbContextOptions<StockSyncDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.GoogleId).IsRequired();
            e.HasIndex(u => u.GoogleId).IsUnique();
            e.Property(u => u.DisplayName).IsRequired();
            e.Property(u => u.Email).IsRequired();
        });

        modelBuilder.Entity<Settings>(e =>
        {
            e.HasKey(s => s.UserId);
            e.HasOne(s => s.User)
            .WithOne(u => u.Settings)
            .HasForeignKey<Settings>(s => s.UserId);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(100);
            e.Property(p => p.Price).IsRequired();
            e.Property(p => p.Quantity).IsRequired();
            e.HasOne(p => p.User)
            .WithMany(u => u.Products)
            .HasForeignKey(p => p.UserId);
        });

        modelBuilder.Entity<Sale>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.ClientSaleId).IsUnique();
            e.HasOne(s => s.User)
            .WithMany(u => u.Sales)
            .HasForeignKey(s => s.UserId);
            e.HasOne(s => s.Product)
            .WithMany(p => p.Sales)
            .HasForeignKey(s => s.ProductId);
        });

        modelBuilder.Entity<StockAdjustment>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.ClientAdjustmentId).IsUnique();
            e.HasOne(a => a.User)
            .WithMany(u => u.StockAdjustments)
            .HasForeignKey(a => a.UserId);
            e.HasOne(a => a.Product)
            .WithMany(p => p.Adjustments)
            .HasForeignKey(a => a.ProductId);
        });
    }
}
