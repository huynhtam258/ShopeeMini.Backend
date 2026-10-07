using ShopeeMini.Domain.Common;

namespace ShopeeMini.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = default!;
    public long? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }

    public ICollection<Category> ChildCategories { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
