using ShopeeMini.Domain.Common;

namespace ShopeeMini.Domain.Entities;

public class OrderItem : BaseEntity
{
    public long OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public long ProductSkuId { get; set; }
    public ProductSku ProductSku { get; set; } = default!;
    public long ShopId { get; set; }

    // Snapshots taken at purchase time so later catalog edits never change a printed invoice.
    public string ProductNameSnapshot { get; set; } = default!;
    public string VariantNameSnapshot { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercentApplied { get; set; }
    public decimal LineTotal { get; set; }
    public int Quantity { get; set; }
}
