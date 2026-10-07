using ShopeeMini.Application.Common.Models;

namespace ShopeeMini.Application.Features.Notifications.Dtos;

public record NotificationQuery : PaginationQuery
{
    public bool? IsRead { get; set; }
}

public record NotificationDto(long Id, string Type, string Title, string Message, bool IsRead, long? RelatedEntityId, DateTime CreatedAt);
