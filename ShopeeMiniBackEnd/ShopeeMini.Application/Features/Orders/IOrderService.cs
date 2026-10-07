using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Features.Orders.Dtos;

namespace ShopeeMini.Application.Features.Orders;

public interface IOrderService
{
    Task<OrderDto> CheckoutAsync(long userId, CancellationToken ct = default);
    Task<PagedResult<OrderListItemDto>> GetMyOrdersAsync(long userId, OrderQuery query, CancellationToken ct = default);
    Task<OrderDto> GetMyOrderDetailAsync(long userId, long orderId, CancellationToken ct = default);
}

public interface IAdminOrderService
{
    Task<PagedResult<AdminOrderLineDto>> GetShopOrdersAsync(long ownerUserId, OrderQuery query, CancellationToken ct = default);
}
