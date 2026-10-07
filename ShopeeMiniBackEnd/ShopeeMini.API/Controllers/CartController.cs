using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopeeMini.Application.Features.Cart;
using ShopeeMini.Application.Features.Cart.Dtos;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.API.Controllers;

[Route("api/cart")]
[Authorize(Roles = nameof(UserRole.User))]
public class CartController : ApiControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService) => _cartService = cartService;

    [HttpGet]
    public async Task<IActionResult> GetCart(CancellationToken ct)
    {
        var result = await _cartService.GetCartAsync(CurrentUserId, ct);
        return SuccessResponse(result);
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem(AddCartItemDto request, CancellationToken ct)
    {
        var result = await _cartService.AddItemAsync(CurrentUserId, request, ct);
        return SuccessResponse(result, "Đã thêm vào giỏ hàng", StatusCodes.Status201Created);
    }

    [HttpPut("items/{cartItemId:long}")]
    public async Task<IActionResult> UpdateItem(long cartItemId, UpdateCartItemDto request, CancellationToken ct)
    {
        var result = await _cartService.UpdateItemAsync(CurrentUserId, cartItemId, request, ct);
        return SuccessResponse(result, "Cập nhật giỏ hàng thành công");
    }

    [HttpDelete("items/{cartItemId:long}")]
    public async Task<IActionResult> RemoveItem(long cartItemId, CancellationToken ct)
    {
        var result = await _cartService.RemoveItemAsync(CurrentUserId, cartItemId, ct);
        return SuccessResponse(result, "Đã xóa khỏi giỏ hàng");
    }
}
