using Microsoft.EntityFrameworkCore;
using ShopeeMini.Application.Common;
using ShopeeMini.Application.Common.Exceptions;
using ShopeeMini.Application.Features.Cart.Dtos;
using ShopeeMini.Application.Interfaces;
using ShopeeMini.Domain.Entities;

namespace ShopeeMini.Application.Features.Cart;

public class CartService : ICartService
{
    private readonly IAppDbContext _db;

    public CartService(IAppDbContext db) => _db = db;

    public async Task<CartSummaryDto> GetCartAsync(long userId, CancellationToken ct = default)
    {
        var items = await LoadCartItemsAsync(userId, ct);
        return BuildSummary(items);
    }

    public async Task<CartSummaryDto> AddItemAsync(long userId, AddCartItemDto request, CancellationToken ct = default)
    {
        var sku = await _db.ProductSkus
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.Id == request.ProductSkuId, ct)
            ?? throw new NotFoundException("SKU", request.ProductSkuId);

        if (!sku.Product.IsPublic)
        {
            throw new BadRequestException("Sản phẩm hiện không khả dụng.");
        }

        var existing = await _db.CartItems
            .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductSkuId == request.ProductSkuId, ct);

        if (existing is not null)
        {
            existing.Quantity += request.Quantity;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.CartItems.Add(new CartItem { UserId = userId, ProductSkuId = request.ProductSkuId, Quantity = request.Quantity });
        }

        await _db.SaveChangesAsync(ct);

        var items = await LoadCartItemsAsync(userId, ct);
        return BuildSummary(items);
    }

    public async Task<CartSummaryDto> UpdateItemAsync(long userId, long cartItemId, UpdateCartItemDto request, CancellationToken ct = default)
    {
        var item = await _db.CartItems.FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId, ct)
            ?? throw new NotFoundException("Sản phẩm trong giỏ hàng", cartItemId);

        item.Quantity = request.Quantity;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var items = await LoadCartItemsAsync(userId, ct);
        return BuildSummary(items);
    }

    public async Task<CartSummaryDto> RemoveItemAsync(long userId, long cartItemId, CancellationToken ct = default)
    {
        var item = await _db.CartItems.FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId, ct)
            ?? throw new NotFoundException("Sản phẩm trong giỏ hàng", cartItemId);

        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync(ct);

        var items = await LoadCartItemsAsync(userId, ct);
        return BuildSummary(items);
    }

    private async Task<List<CartItem>> LoadCartItemsAsync(long userId, CancellationToken ct)
    {
        return await _db.CartItems.AsNoTracking()
            .Include(ci => ci.ProductSku).ThenInclude(s => s.Product)
            .Include(ci => ci.ProductSku).ThenInclude(s => s.Discounts)
            .Where(ci => ci.UserId == userId)
            .OrderByDescending(ci => ci.CreatedAt)
            .ToListAsync(ct);
    }

    private static CartSummaryDto BuildSummary(List<CartItem> items)
    {
        var now = DateTime.UtcNow;
        var dtos = items.Select(ci =>
        {
            var discountPercent = DiscountCalculator.GetActiveDiscountPercent(ci.ProductSku.Discounts, now);
            var effectivePrice = DiscountCalculator.ApplyDiscount(ci.ProductSku.Price, discountPercent);
            var lineTotal = effectivePrice * ci.Quantity;

            return new CartItemDto(
                ci.Id,
                ci.ProductSkuId,
                ci.ProductSku.ProductId,
                ci.ProductSku.Product.Name,
                ci.ProductSku.VariantName,
                ci.ProductSku.Price,
                effectivePrice,
                discountPercent,
                ci.Quantity,
                lineTotal,
                ci.ProductSku.StockQuantity);
        }).ToList();

        return new CartSummaryDto(dtos, dtos.Sum(d => d.LineTotal));
    }
}
