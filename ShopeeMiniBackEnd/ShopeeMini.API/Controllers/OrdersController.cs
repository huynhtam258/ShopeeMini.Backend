using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopeeMini.API.Filters;
using ShopeeMini.Application.Features.Orders;
using ShopeeMini.Application.Features.Orders.Dtos;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.API.Controllers;

[Route("api/orders")]
[Authorize(Roles = nameof(UserRole.User))]
public class OrdersController : ApiControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService) => _orderService = orderService;

    [HttpPost("checkout")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    public async Task<IActionResult> Checkout(CancellationToken ct)
    {
        var result = await _orderService.CheckoutAsync(CurrentUserId, ct);
        return SuccessResponse(result, "Thanh toán thành công", StatusCodes.Status201Created);
    }

    /// <summary>
    /// DEMO ONLY: the exact same checkout as <see cref="Checkout"/> but WITHOUT <see cref="IdempotencyFilter"/>.
    /// Exists so the front-end can show what goes wrong when a client retries a checkout whose
    /// response was lost: the retry re-runs CheckoutAsync instead of replaying the first result.
    /// </summary>
    [HttpPost("checkout-no-idempotency")]
    public async Task<IActionResult> CheckoutNoIdempotency(CancellationToken ct)
    {
        var result = await _orderService.CheckoutAsync(CurrentUserId, ct);
        return SuccessResponse(result, "Thanh toán thành công", StatusCodes.Status201Created);
    }

    [HttpGet]
    public async Task<IActionResult> GetMyOrders([FromQuery] OrderQuery query, CancellationToken ct)
    {
        var result = await _orderService.GetMyOrdersAsync(CurrentUserId, query, ct);
        return SuccessResponse(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetOrderDetail(long id, CancellationToken ct)
    {
        var result = await _orderService.GetMyOrderDetailAsync(CurrentUserId, id, ct);
        return SuccessResponse(result);
    }
}

[Route("api/admin/orders")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminOrdersController : ApiControllerBase
{
    private readonly IAdminOrderService _adminOrderService;

    public AdminOrdersController(IAdminOrderService adminOrderService) => _adminOrderService = adminOrderService;

    [HttpGet]
    public async Task<IActionResult> GetShopOrders([FromQuery] OrderQuery query, CancellationToken ct)
    {
        var result = await _adminOrderService.GetShopOrdersAsync(CurrentUserId, query, ct);
        return SuccessResponse(result);
    }
}
