using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopeeMini.Domain.Entities;

namespace ShopeeMini.Infrastructure.Persistence.Configurations;

public class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("Shops");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);

        builder.HasOne(s => s.Owner)
            .WithOne(u => u.Shop)
            .HasForeignKey<Shop>(s => s.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.OwnerUserId).IsUnique();
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);

        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.ChildCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(250);
        builder.Property(p => p.Description).HasMaxLength(4000);
        builder.HasIndex(p => new { p.ShopId, p.IsPublic });
        builder.HasIndex(p => p.CategoryId);

        builder.HasOne(p => p.Shop)
            .WithMany(s => s.Products)
            .HasForeignKey(p => p.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductSkuConfiguration : IEntityTypeConfiguration<ProductSku>
{
    public void Configure(EntityTypeBuilder<ProductSku> builder)
    {
        builder.ToTable("ProductSkus");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SkuCode).IsRequired().HasMaxLength(64);
        builder.HasIndex(s => s.SkuCode).IsUnique();
        builder.Property(s => s.VariantName).IsRequired().HasMaxLength(150);
        builder.Property(s => s.Price).HasPrecision(18, 2);

        builder.HasOne(s => s.Product)
            .WithMany(p => p.Skus)
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductDiscountConfiguration : IEntityTypeConfiguration<ProductDiscount>
{
    public void Configure(EntityTypeBuilder<ProductDiscount> builder)
    {
        builder.ToTable("ProductDiscounts");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.DiscountPercent).HasPrecision(5, 2);
        builder.HasIndex(d => new { d.ProductSkuId, d.IsActive, d.StartAt, d.EndAt });

        builder.HasOne(d => d.ProductSku)
            .WithMany(s => s.Discounts)
            .HasForeignKey(d => d.ProductSkuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
