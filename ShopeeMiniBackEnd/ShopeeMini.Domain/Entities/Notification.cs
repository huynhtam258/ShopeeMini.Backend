using ShopeeMini.Domain.Common;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.Domain.Entities;

public class Notification : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = default!;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public bool IsRead { get; set; } = false;
    public long? RelatedEntityId { get; set; }
}
