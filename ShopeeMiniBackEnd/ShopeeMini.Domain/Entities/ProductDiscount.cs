using ShopeeMini.Domain.Common;

namespace ShopeeMini.Domain.Entities;

/// <summary>Time-boxed discount applied to a single SKU. Never applies to the whole order.</summary>
public class ProductDiscount : BaseEntity
{
    public long ProductSkuId { get; set; }
    public ProductSku ProductSku { get; set; } = default!;
    public decimal DiscountPercent { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsActive { get; set; } = true;
}
