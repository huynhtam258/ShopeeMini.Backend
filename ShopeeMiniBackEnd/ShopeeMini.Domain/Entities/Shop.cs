using ShopeeMini.Domain.Common;

namespace ShopeeMini.Domain.Entities;

public class Shop : BaseEntity
{
    public long OwnerUserId { get; set; }
    public User Owner { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
