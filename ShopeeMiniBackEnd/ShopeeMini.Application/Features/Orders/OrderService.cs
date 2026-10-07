using Microsoft.EntityFrameworkCore;
using ShopeeMini.Application.Common;
using ShopeeMini.Application.Common.Exceptions;
using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Features.Orders.Dtos;
using ShopeeMini.Application.Interfaces;
using ShopeeMini.Domain.Entities;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.Application.Features.Orders;

public class OrderService : IOrderService
{
    private readonly IAppDbContext _db;
    private readonly IInvoiceJsonWriter _invoiceWriter;

    public OrderService(IAppDbContext db, IInvoiceJsonWriter invoiceWriter)
    {
        _db = db;
        _invoiceWriter = invoiceWriter;
    }

    public async Task<OrderDto> CheckoutAsync(long userId, CancellationToken ct = default)
    {
        var cartItems = await _db.CartItems
            .Include(ci => ci.ProductSku).ThenInclude(s => s.Product).ThenInclude(p => p.Shop)
            .Include(ci => ci.ProductSku).ThenInclude(s => s.Discounts)
            .Where(ci => ci.UserId == userId)
            .ToListAsync(ct);

        if (cartItems.Count == 0)
        {
            throw new BadRequestException("Giỏ hàng đang trống.");
        }

        var now = DateTime.UtcNow;

        // --- ACID demo: the stock reservation loop below runs inside one DB transaction.
        // Each reservation is a single atomic, conditional UPDATE (StockQuantity >= Quantity),
        // so two concurrent checkouts racing for the last unit are serialized by the database's
        // own row lock — only one UPDATE can see stock as sufficient and affect a row.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        ProductSku? outOfStockSku = null;
        foreach (var item in cartItems)
        {
            var affectedRows = await _db.ProductSkus
                .Where(s => s.Id == item.ProductSkuId && s.StockQuantity >= item.Quantity)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.StockQuantity, s => s.StockQuantity - item.Quantity), ct);

            if (affectedRows == 0)
            {
                outOfStockSku = item.ProductSku;
                break;
            }
        }

        if (outOfStockSku is not null)
        {
            // Roll back every reservation already made in this loop — all-or-nothing (Atomicity).
            await transaction.RollbackAsync(ct);
            await RecordFailedOrderAsync(userId, cartItems, outOfStockSku, ct);
            throw new ConflictException($"Sản phẩm '{outOfStockSku.VariantName}' không đủ tồn kho để thanh toán.");
        }

        var order = new Order
        {
            UserId = userId,
            OrderCode = GenerateOrderCode(),
            Status = OrderStatus.Paid
        };

        decimal total = 0m;
        foreach (var item in cartItems)
        {
            var discountPercent = DiscountCalculator.GetActiveDiscountPercent(item.ProductSku.Discounts, now);
            var unitPrice = item.ProductSku.Price;
            var lineTotal = DiscountCalculator.ApplyDiscount(unitPrice, discountPercent) * item.Quantity;
            total += lineTotal;

            order.Items.Add(new OrderItem
            {
                ProductSkuId = item.ProductSkuId,
                ShopId = item.ProductSku.Product.ShopId,
                ProductNameSnapshot = item.ProductSku.Product.Name,
                VariantNameSnapshot = item.ProductSku.VariantName,
                UnitPrice = unitPrice,
                DiscountPercentApplied = discountPercent,
                LineTotal = lineTotal,
                Quantity = item.Quantity
            });
        }

        order.TotalAmount = total;
        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cartItems);

        // Persist the order first so notifications below can reference its generated Id.
        await _db.SaveChangesAsync(ct);

        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = NotificationType.OrderPlacedSuccess,
            Title = "Đặt hàng thành công",
            Message = $"Đơn hàng {order.OrderCode} đã được thanh toán thành công. Tổng tiền: {total:N0}đ.",
            RelatedEntityId = order.Id
        });

        foreach (var shopId in order.Items.Select(i => i.ShopId).Distinct())
        {
            var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct);
            if (shop is not null)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = shop.OwnerUserId,
                    Type = NotificationType.NewOrderForShop,
                    Title = "Có đơn hàng mới",
                    Message = $"Shop của bạn có đơn hàng mới: {order.OrderCode}.",
                    RelatedEntityId = order.Id
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // File + DB feature: write an immutable JSON invoice snapshot after the DB transaction
        // has committed. This I/O is deliberately outside the transaction — disk writes cannot
        // be rolled back by the database, so they must never gate the commit.
        var invoicePath = await _invoiceWriter.WriteInvoiceAsync(order, ct);
        order.InvoiceJsonPath = invoicePath;
        await _db.SaveChangesAsync(ct);

        return MapToDto(order);
    }

    public async Task<PagedResult<OrderListItemDto>> GetMyOrdersAsync(long userId, OrderQuery query, CancellationToken ct = default)
    {
        var status = ParseStatus(query.Status);

        var baseQuery = _db.Orders.AsNoTracking()
            .Where(o => o.UserId == userId)
            .Where(o => status == null || o.Status == status)
            .Where(o => query.FromDate == null || o.CreatedAt >= query.FromDate)
            .Where(o => query.ToDate == null || o.CreatedAt <= query.ToDate);

        var totalCount = await baseQuery.LongCountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(o => o.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(o => new OrderListItemDto(o.Id, o.OrderCode, o.Status.ToString(), o.TotalAmount, o.Items.Count, o.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<OrderListItemDto> { Items = items, Page = query.Page, PageSize = query.PageSize, TotalCount = totalCount };
    }

    public async Task<OrderDto> GetMyOrderDetailAsync(long userId, long orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct)
            ?? throw new NotFoundException("Đơn hàng", orderId);

        return MapToDto(order);
    }

    private async Task RecordFailedOrderAsync(long userId, List<CartItem> cartItems, ProductSku outOfStockSku, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var order = new Order { UserId = userId, OrderCode = GenerateOrderCode(), Status = OrderStatus.Failed, TotalAmount = 0 };

        foreach (var item in cartItems)
        {
            var discountPercent = DiscountCalculator.GetActiveDiscountPercent(item.ProductSku.Discounts, now);
            order.Items.Add(new OrderItem
            {
                ProductSkuId = item.ProductSkuId,
                ShopId = item.ProductSku.Product.ShopId,
                ProductNameSnapshot = item.ProductSku.Product.Name,
                VariantNameSnapshot = item.ProductSku.VariantName,
                UnitPrice = item.ProductSku.Price,
                DiscountPercentApplied = discountPercent,
                LineTotal = 0,
                Quantity = item.Quantity
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = NotificationType.OrderPlacedFailed,
            Title = "Thanh toán thất bại",
            Message = $"Đơn hàng {order.OrderCode} thất bại: sản phẩm '{outOfStockSku.VariantName}' không đủ tồn kho.",
            RelatedEntityId = order.Id
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string GenerateOrderCode() =>
        $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";

    private static OrderStatus? ParseStatus(string? status) =>
        Enum.TryParse<OrderStatus>(status, true, out var parsed) ? parsed : null;

    private static OrderDto MapToDto(Order order) => new(
        order.Id,
        order.OrderCode,
        order.Status.ToString(),
        order.TotalAmount,
        order.CreatedAt,
        order.InvoiceJsonPath,
        order.Items.Select(i => new OrderItemDto(
            i.ProductSkuId, i.ShopId, i.ProductNameSnapshot, i.VariantNameSnapshot,
            i.UnitPrice, i.DiscountPercentApplied, i.Quantity, i.LineTotal)).ToList());
}
