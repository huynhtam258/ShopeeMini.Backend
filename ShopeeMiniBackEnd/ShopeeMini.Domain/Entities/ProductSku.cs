using ShopeeMini.Domain.Common;

namespace ShopeeMini.Domain.Entities;

/// <summary>SKU — a concrete purchasable variant of a Product (SPU), carrying its own price and stock.</summary>
public class ProductSku : BaseEntity
{
    public long ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public string SkuCode { get; set; } = default!;
    public string VariantName { get; set; } = default!;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }

    public ICollection<ProductDiscount> Discounts { get; set; } = new List<ProductDiscount>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
