using ShopeeMini.Domain.Common;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.Domain.Entities;

public class Order : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = default!;
    public string OrderCode { get; set; } = default!;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>Path to the JSON invoice snapshot written to disk at checkout time.</summary>
    public string? InvoiceJsonPath { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
