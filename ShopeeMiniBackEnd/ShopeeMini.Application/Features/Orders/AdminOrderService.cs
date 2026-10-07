using Microsoft.EntityFrameworkCore;
using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Features.Orders.Dtos;
using ShopeeMini.Application.Interfaces;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.Application.Features.Orders;

public class AdminOrderService : IAdminOrderService
{
    private readonly IAppDbContext _db;

    public AdminOrderService(IAppDbContext db) => _db = db;

    public async Task<PagedResult<AdminOrderLineDto>> GetShopOrdersAsync(long ownerUserId, OrderQuery query, CancellationToken ct = default)
    {
        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == ownerUserId, ct);
        if (shop is null)
        {
            return new PagedResult<AdminOrderLineDto> { Items = Array.Empty<AdminOrderLineDto>(), Page = query.Page, PageSize = query.PageSize, TotalCount = 0 };
        }

        var status = Enum.TryParse<OrderStatus>(query.Status, true, out var parsedStatus) ? parsedStatus : (OrderStatus?)null;

        var baseQuery = _db.OrderItems.AsNoTracking()
            .Where(oi => oi.ShopId == shop.Id)
            .Where(oi => status == null || oi.Order.Status == status)
            .Where(oi => query.FromDate == null || oi.CreatedAt >= query.FromDate)
            .Where(oi => query.ToDate == null || oi.CreatedAt <= query.ToDate);

        var totalCount = await baseQuery.LongCountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(oi => oi.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(oi => new AdminOrderLineDto(
                oi.OrderId,
                oi.Order.OrderCode,
                oi.Order.Status.ToString(),
                oi.Order.User.FullName,
                oi.Order.User.Email,
                oi.ProductNameSnapshot,
                oi.VariantNameSnapshot,
                oi.UnitPrice,
                oi.DiscountPercentApplied,
                oi.Quantity,
                oi.LineTotal,
                oi.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<AdminOrderLineDto> { Items = items, Page = query.Page, PageSize = query.PageSize, TotalCount = totalCount };
    }
}
