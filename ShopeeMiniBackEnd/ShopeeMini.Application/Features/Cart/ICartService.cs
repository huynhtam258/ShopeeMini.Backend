using ShopeeMini.Application.Features.Cart.Dtos;

namespace ShopeeMini.Application.Features.Cart;

public interface ICartService
{
    Task<CartSummaryDto> GetCartAsync(long userId, CancellationToken ct = default);
    Task<CartSummaryDto> AddItemAsync(long userId, AddCartItemDto request, CancellationToken ct = default);
    Task<CartSummaryDto> UpdateItemAsync(long userId, long cartItemId, UpdateCartItemDto request, CancellationToken ct = default);
    Task<CartSummaryDto> RemoveItemAsync(long userId, long cartItemId, CancellationToken ct = default);
}
