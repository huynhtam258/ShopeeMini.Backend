using ShopeeMini.Domain.Common;

namespace ShopeeMini.Domain.Entities;

/// <summary>SPU — the "product" shell that groups one or more SKUs (variants).</summary>
public class Product : BaseEntity
{
    public long ShopId { get; set; }
    public Shop Shop { get; set; } = default!;
    public long CategoryId { get; set; }
    public Category Category { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsPublic { get; set; } = false;

    public ICollection<ProductSku> Skus { get; set; } = new List<ProductSku>();
}
