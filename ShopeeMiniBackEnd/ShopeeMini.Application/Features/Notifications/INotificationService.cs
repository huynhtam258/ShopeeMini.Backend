using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Features.Notifications.Dtos;

namespace ShopeeMini.Application.Features.Notifications;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetMyNotificationsAsync(long userId, NotificationQuery query, CancellationToken ct = default);
    Task MarkAsReadAsync(long userId, long notificationId, CancellationToken ct = default);
    Task DeleteAsync(long userId, long notificationId, CancellationToken ct = default);
}
