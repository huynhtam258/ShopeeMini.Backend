using ShopeeMini.Application.Common.Models;

namespace ShopeeMini.Application.Features.Orders.Dtos;

public record OrderQuery : PaginationQuery
{
    public string? Status { get; set; }
}

public record OrderItemDto(
    long ProductSkuId,
    long ShopId,
    string ProductName,
    string VariantName,
    decimal UnitPrice,
    decimal DiscountPercentApplied,
    int Quantity,
    decimal LineTotal);

public record OrderDto(
    long Id,
    string OrderCode,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    string? InvoiceJsonPath,
    IReadOnlyList<OrderItemDto> Items);

public record OrderListItemDto(long Id, string OrderCode, string Status, decimal TotalAmount, int ItemCount, DateTime CreatedAt);

public record AdminOrderLineDto(
    long OrderId,
    string OrderCode,
    string OrderStatus,
    string BuyerName,
    string BuyerEmail,
    string ProductName,
    string VariantName,
    decimal UnitPrice,
    decimal DiscountPercentApplied,
    int Quantity,
    decimal LineTotal,
    DateTime CreatedAt);
