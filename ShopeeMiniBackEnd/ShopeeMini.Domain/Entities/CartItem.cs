using ShopeeMini.Domain.Common;

namespace ShopeeMini.Domain.Entities;

public class CartItem : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = default!;
    public long ProductSkuId { get; set; }
    public ProductSku ProductSku { get; set; } = default!;
    public int Quantity { get; set; }
}
