using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopeeMini.Domain.Entities;

namespace ShopeeMini.Infrastructure.Persistence.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems");
        builder.HasKey(ci => ci.Id);
        builder.HasIndex(ci => new { ci.UserId, ci.ProductSkuId }).IsUnique();

        builder.HasOne(ci => ci.User)
            .WithMany(u => u.CartItems)
            .HasForeignKey(ci => ci.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ci => ci.ProductSku)
            .WithMany(s => s.CartItems)
            .HasForeignKey(ci => ci.ProductSkuId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.OrderCode).IsRequired().HasMaxLength(40);
        builder.HasIndex(o => o.OrderCode).IsUnique();
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.InvoiceJsonPath).HasMaxLength(500);
        builder.HasIndex(o => new { o.UserId, o.CreatedAt });

        builder.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(oi => oi.Id);
        builder.Property(oi => oi.ProductNameSnapshot).IsRequired().HasMaxLength(250);
        builder.Property(oi => oi.VariantNameSnapshot).IsRequired().HasMaxLength(150);
        builder.Property(oi => oi.UnitPrice).HasPrecision(18, 2);
        builder.Property(oi => oi.DiscountPercentApplied).HasPrecision(5, 2);
        builder.Property(oi => oi.LineTotal).HasPrecision(18, 2);
        builder.HasIndex(oi => new { oi.ShopId, oi.CreatedAt });

        builder.HasOne(oi => oi.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(oi => oi.ProductSku)
            .WithMany(s => s.OrderItems)
            .HasForeignKey(oi => oi.ProductSkuId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Message).IsRequired().HasMaxLength(1000);
        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Key).IsRequired().HasMaxLength(128);
        builder.Property(r => r.RequestPath).IsRequired().HasMaxLength(256);
        builder.Property(r => r.ResponseBody).HasColumnType("longtext");
        builder.HasIndex(r => new { r.UserId, r.Key, r.RequestPath }).IsUnique();
    }
}
