using Microsoft.EntityFrameworkCore;

public class ShopAxisDbContext : DbContext
{
    // ── Tables ────────────────────────────────────────────────────────────
    public DbSet<OrderRecord> Orders { get; set; }
    public DbSet<OrderItem>   OrderItems { get; set; }
    public DbSet<ReturnRecord> Returns { get; set; }
    public DbSet<ReturnItem>  ReturnItems { get; set; }
    public DbSet<AuditEntry>  AuditLog { get; set; }

    // ── Constructor ───────────────────────────────────────────────────────
    public ShopAxisDbContext(DbContextOptions<ShopAxisDbContext> options)
        : base(options) { }

    // ── Fluent API configuration ──────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Orders → OrderItems (one to many)
        modelBuilder.Entity<OrderRecord>()
            .HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Orders → Returns (one to many)
        modelBuilder.Entity<ReturnRecord>()
            .HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Returns → ReturnItems (one to many)
        modelBuilder.Entity<ReturnRecord>()
            .HasMany(r => r.ReturnItems)
            .WithOne(i => i.Return)
            .HasForeignKey(i => i.RmaNumber)
            .OnDelete(DeleteBehavior.Cascade);

        // DateOnly support — EF Core needs this for SQL Server
        modelBuilder.Entity<OrderRecord>()
            .Property(o => o.EstimatedDelivery)
            .HasColumnType("date");

        modelBuilder.Entity<OrderRecord>()
            .Property(o => o.DeliveryDate)
            .HasColumnType("date");

        modelBuilder.Entity<ReturnRecord>()
            .Property(r => r.ExpectedCompletion)
            .HasColumnType("date");

        // ── Seed data — relative dates always valid ───────────────────────
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Note: seed data needs fixed dates for migrations
        // Use a fixed "demo" date that's always valid for testing
        var today = new DateOnly(2026, 4, 29);

        modelBuilder.Entity<OrderRecord>().HasData(
            new OrderRecord
            {
                OrderId           = "ORD-00482917",
                CustomerEmail     = "sarah.chen@example.com",
                Status            = "shipped",
                Carrier           = "FedEx",
                TrackingNumber    = "794644792798",
                EstimatedDelivery = today.AddDays(3),
                DeliveryDate      = null
            },
            new OrderRecord
            {
                OrderId           = "ORD-00391045",
                CustomerEmail     = "james.park@example.com",
                Status            = "delivered",
                Carrier           = "UPS",
                TrackingNumber    = "1Z999AA10123456784",
                EstimatedDelivery = today.AddDays(-35),
                DeliveryDate      = today.AddDays(-35)
            },
            new OrderRecord
            {
                OrderId           = "ORD-00512334",
                CustomerEmail     = "angry@example.com",
                Status            = "out_for_delivery",
                Carrier           = "DHL",
                TrackingNumber    = "1234567890",
                EstimatedDelivery = today.AddDays(1),
                DeliveryDate      = null
            },
            new OrderRecord
            {
                OrderId           = "ORD-00734521",
                CustomerEmail     = "lisa.wang@example.com",
                Status            = "delivered",
                Carrier           = "UPS",
                TrackingNumber    = "1Z999AA10987654321",
                EstimatedDelivery = today.AddDays(-20),
                DeliveryDate      = today.AddDays(-20)
            }
        );

        modelBuilder.Entity<OrderItem>().HasData(
            new OrderItem { Id = 1, OrderId = "ORD-00482917", Sku = "SKU-1042", Name = "Wireless Headphones",  Qty = 1, Price = 89.99m },
            new OrderItem { Id = 2, OrderId = "ORD-00391045", Sku = "SKU-2087", Name = "Standing Desk Mat",    Qty = 1, Price = 45.00m },
            new OrderItem { Id = 3, OrderId = "ORD-00512334", Sku = "SKU-3301", Name = "Mechanical Keyboard",  Qty = 1, Price = 129.99m },
            new OrderItem { Id = 4, OrderId = "ORD-00734521", Sku = "SKU-5521", Name = "USB-C Hub",            Qty = 2, Price = 35.00m },
            new OrderItem { Id = 5, OrderId = "ORD-00734521", Sku = "SKU-5522", Name = "Laptop Stand",         Qty = 1, Price = 59.99m }
        );
    }
}