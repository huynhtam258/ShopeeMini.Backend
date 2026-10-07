using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopeeMini.Application.Features.Notifications;
using ShopeeMini.Application.Features.Notifications.Dtos;

namespace ShopeeMini.API.Controllers;

[Route("api/notifications")]
[Authorize]
public class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService) => _notificationService = notificationService;

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications([FromQuery] NotificationQuery query, CancellationToken ct)
    {
        var result = await _notificationService.GetMyNotificationsAsync(CurrentUserId, query, ct);
        return SuccessResponse(result);
    }

    [HttpPut("{id:long}/read")]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken ct)
    {
        await _notificationService.MarkAsReadAsync(CurrentUserId, id, ct);
        return SuccessResponse("Đã đánh dấu đã đọc");
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _notificationService.DeleteAsync(CurrentUserId, id, ct);
        return SuccessResponse("Đã xóa thông báo");
    }
}
