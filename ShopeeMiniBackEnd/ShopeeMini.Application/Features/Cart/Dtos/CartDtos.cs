namespace ShopeeMini.Application.Features.Cart.Dtos;

public record AddCartItemDto(long ProductSkuId, int Quantity);

public record UpdateCartItemDto(int Quantity);

public record CartItemDto(
    long Id,
    long ProductSkuId,
    long ProductId,
    string ProductName,
    string VariantName,
    decimal UnitPrice,
    decimal EffectivePrice,
    decimal DiscountPercent,
    int Quantity,
    decimal LineTotal,
    int AvailableStock);

public record CartSummaryDto(IReadOnlyList<CartItemDto> Items, decimal TotalAmount);
