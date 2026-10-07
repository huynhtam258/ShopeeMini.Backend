using Microsoft.EntityFrameworkCore;
using ShopeeMini.Application.Common.Exceptions;
using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Features.Notifications.Dtos;
using ShopeeMini.Application.Interfaces;

namespace ShopeeMini.Application.Features.Notifications;

public class NotificationService : INotificationService
{
    private readonly IAppDbContext _db;

    public NotificationService(IAppDbContext db) => _db = db;

    public async Task<PagedResult<NotificationDto>> GetMyNotificationsAsync(long userId, NotificationQuery query, CancellationToken ct = default)
    {
        var baseQuery = _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .Where(n => query.IsRead == null || n.IsRead == query.IsRead)
            .Where(n => query.FromDate == null || n.CreatedAt >= query.FromDate)
            .Where(n => query.ToDate == null || n.CreatedAt <= query.ToDate);

        var totalCount = await baseQuery.LongCountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(n => n.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(n => new NotificationDto(n.Id, n.Type.ToString(), n.Title, n.Message, n.IsRead, n.RelatedEntityId, n.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<NotificationDto> { Items = items, Page = query.Page, PageSize = query.PageSize, TotalCount = totalCount };
    }

    public async Task MarkAsReadAsync(long userId, long notificationId, CancellationToken ct = default)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct)
            ?? throw new NotFoundException("Thông báo", notificationId);

        notification.IsRead = true;
        notification.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long userId, long notificationId, CancellationToken ct = default)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct)
            ?? throw new NotFoundException("Thông báo", notificationId);

        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync(ct);
    }
}
