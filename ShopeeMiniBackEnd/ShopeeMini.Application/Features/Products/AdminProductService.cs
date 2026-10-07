using Microsoft.EntityFrameworkCore;
using ShopeeMini.Application.Common;
using ShopeeMini.Application.Common.Exceptions;
using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Features.Products.Dtos;
using ShopeeMini.Application.Interfaces;
using ShopeeMini.Domain.Entities;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.Application.Features.Products;

public class AdminProductService : IAdminProductService
{
    private readonly IAppDbContext _db;

    public AdminProductService(IAppDbContext db) => _db = db;

    private async Task<Shop> GetOrCreateShopAsync(long ownerUserId, CancellationToken ct)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.OwnerUserId == ownerUserId, ct);
        if (shop is not null)
        {
            return shop;
        }

        var owner = await _db.Users.FirstOrDefaultAsync(u => u.Id == ownerUserId, ct)
            ?? throw new NotFoundException("Người dùng", ownerUserId);

        shop = new Shop { OwnerUserId = ownerUserId, Name = $"{owner.FullName}'s Shop" };
        _db.Shops.Add(shop);
        await _db.SaveChangesAsync(ct);
        return shop;
    }

    private async Task<Product> GetOwnedProductAsync(long ownerUserId, long productId, CancellationToken ct, bool includeSkus = true)
    {
        var query = _db.Products.Include(p => p.Shop).Include(p => p.Category).AsQueryable();
        if (includeSkus)
        {
            query = query.Include(p => p.Skus).ThenInclude(s => s.Discounts);
        }

        var product = await query.FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new NotFoundException("Sản phẩm", productId);

        if (product.Shop.OwnerUserId != ownerUserId)
        {
            throw new ForbiddenException("Sản phẩm không thuộc shop của bạn.");
        }

        return product;
    }

    public async Task<PagedResult<ProductListItemDto>> GetMyProductsAsync(long ownerUserId, ProductQuery query, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var shop = await GetOrCreateShopAsync(ownerUserId, ct);

        var baseQuery = _db.Products.AsNoTracking()
            .Where(p => p.ShopId == shop.Id)
            .Where(p => query.CategoryId == null || p.CategoryId == query.CategoryId)
            .Where(p => query.Search == null || EF.Functions.Like(p.Name, $"%{query.Search}%"))
            .Where(p => query.FromDate == null || p.CreatedAt >= query.FromDate)
            .Where(p => query.ToDate == null || p.CreatedAt <= query.ToDate);

        var totalCount = await baseQuery.LongCountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(p => p.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(p => new ProductListItemDto(
                p.Id,
                p.Name,
                p.Category.Name,
                p.IsPublic,
                p.Skus.Min(s => (decimal?)s.Price) ?? 0m,
                p.Skus.Max(s => (decimal?)(s.Price * (1 - (s.Discounts
                    .Where(d => d.IsActive && d.StartAt <= now && d.EndAt >= now)
                    .Select(d => (decimal?)d.DiscountPercent).Max() ?? 0m) / 100m))) ?? 0m,
                p.Skus.Min(s => (decimal?)(s.Price * (1 - (s.Discounts
                    .Where(d => d.IsActive && d.StartAt <= now && d.EndAt >= now)
                    .Select(d => (decimal?)d.DiscountPercent).Max() ?? 0m) / 100m))) ?? 0m,
                p.Skus.Any(s => s.Discounts.Any(d => d.IsActive && d.StartAt <= now && d.EndAt >= now)),
                p.CreatedAt)
            )
            .ToListAsync(ct);

        return new PagedResult<ProductListItemDto> { Items = items, Page = query.Page, PageSize = query.PageSize, TotalCount = totalCount };
    }

    public async Task<ProductDetailDto> GetProductForAdminAsync(long ownerUserId, long productId, CancellationToken ct = default)
    {
        var product = await GetOwnedProductAsync(ownerUserId, productId, ct);
        return MapDetail(product);
    }

    public async Task<ProductDetailDto> CreateProductAsync(long ownerUserId, CreateProductDto request, CancellationToken ct = default)
    {
        var shop = await GetOrCreateShopAsync(ownerUserId, ct);

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct)
            ?? throw new NotFoundException("Danh mục", request.CategoryId);

        var product = new Product
        {
            ShopId = shop.Id,
            CategoryId = request.CategoryId,
            Name = request.Name,
            Description = request.Description,
            IsPublic = false,
            Skus = request.Skus.Select(s => new ProductSku
            {
                SkuCode = s.SkuCode,
                VariantName = s.VariantName,
                Price = s.Price,
                StockQuantity = s.StockQuantity
            }).ToList()
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        product.Shop = shop;
        product.Category = category;
        return MapDetail(product);
    }

    public async Task<ProductDetailDto> UpdateProductAsync(long ownerUserId, long productId, UpdateProductDto request, CancellationToken ct = default)
    {
        var product = await GetOwnedProductAsync(ownerUserId, productId, ct);

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct)
            ?? throw new NotFoundException("Danh mục", request.CategoryId);

        product.Name = request.Name;
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.Category = category;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return MapDetail(product);
    }

    public async Task SetProductStatusAsync(long ownerUserId, long productId, bool isPublic, CancellationToken ct = default)
    {
        var product = await GetOwnedProductAsync(ownerUserId, productId, ct);

        if (product.IsPublic == isPublic)
        {
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        product.IsPublic = isPublic;
        product.UpdatedAt = DateTime.UtcNow;

        if (!isPublic)
        {
            var skuIds = product.Skus.Select(s => s.Id).ToList();

            var affectedCartItems = await _db.CartItems
                .Include(ci => ci.User)
                .Where(ci => skuIds.Contains(ci.ProductSkuId))
                .ToListAsync(ct);

            var affectedUserIds = affectedCartItems.Select(ci => ci.UserId).Distinct().ToList();

            _db.CartItems.RemoveRange(affectedCartItems);

            foreach (var userId in affectedUserIds)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.ProductHiddenRemovedFromCart,
                    Title = "Sản phẩm trong giỏ hàng đã ngừng bán",
                    Message = $"Sản phẩm '{product.Name}' đã bị ẩn khỏi cửa hàng và được tự động xóa khỏi giỏ hàng của bạn.",
                    RelatedEntityId = product.Id
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<SkuDto> AddSkuAsync(long ownerUserId, long productId, CreateSkuDto request, CancellationToken ct = default)
    {
        var product = await GetOwnedProductAsync(ownerUserId, productId, ct);

        var sku = new ProductSku
        {
            ProductId = product.Id,
            SkuCode = request.SkuCode,
            VariantName = request.VariantName,
            Price = request.Price,
            StockQuantity = request.StockQuantity
        };

        _db.ProductSkus.Add(sku);
        await _db.SaveChangesAsync(ct);

        return new SkuDto(sku.Id, sku.SkuCode, sku.VariantName, sku.Price, sku.Price, 0, sku.StockQuantity);
    }

    public async Task<SkuDto> UpdateSkuStockAsync(long ownerUserId, long skuId, UpdateStockDto request, CancellationToken ct = default)
    {
        var sku = await GetOwnedSkuAsync(ownerUserId, skuId, ct);
        sku.StockQuantity = request.StockQuantity;
        sku.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var discountPercent = DiscountCalculator.GetActiveDiscountPercent(sku.Discounts, DateTime.UtcNow);
        return new SkuDto(sku.Id, sku.SkuCode, sku.VariantName, sku.Price, DiscountCalculator.ApplyDiscount(sku.Price, discountPercent), discountPercent, sku.StockQuantity);
    }

    public async Task<SkuDto> UpdateSkuPriceAsync(long ownerUserId, long skuId, UpdateSkuPriceDto request, CancellationToken ct = default)
    {
        var sku = await GetOwnedSkuAsync(ownerUserId, skuId, ct);
        sku.Price = request.Price;
        sku.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var discountPercent = DiscountCalculator.GetActiveDiscountPercent(sku.Discounts, DateTime.UtcNow);
        return new SkuDto(sku.Id, sku.SkuCode, sku.VariantName, sku.Price, DiscountCalculator.ApplyDiscount(sku.Price, discountPercent), discountPercent, sku.StockQuantity);
    }

    public async Task<DiscountDto> CreateDiscountAsync(long ownerUserId, CreateDiscountDto request, CancellationToken ct = default)
    {
        var sku = await GetOwnedSkuAsync(ownerUserId, request.ProductSkuId, ct);

        var discount = new ProductDiscount
        {
            ProductSkuId = sku.Id,
            DiscountPercent = request.DiscountPercent,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            IsActive = true
        };

        _db.ProductDiscounts.Add(discount);
        await _db.SaveChangesAsync(ct);

        return new DiscountDto(discount.Id, sku.Id, sku.VariantName, discount.DiscountPercent, discount.StartAt, discount.EndAt, discount.IsActive);
    }

    public async Task DeactivateDiscountAsync(long ownerUserId, long discountId, CancellationToken ct = default)
    {
        var discount = await _db.ProductDiscounts
            .Include(d => d.ProductSku).ThenInclude(s => s.Product).ThenInclude(p => p.Shop)
            .FirstOrDefaultAsync(d => d.Id == discountId, ct)
            ?? throw new NotFoundException("Khuyến mãi", discountId);

        if (discount.ProductSku.Product.Shop.OwnerUserId != ownerUserId)
        {
            throw new ForbiddenException("Khuyến mãi không thuộc shop của bạn.");
        }

        discount.IsActive = false;
        discount.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DiscountDto>> GetMyDiscountsAsync(long ownerUserId, CancellationToken ct = default)
    {
        var shop = await GetOrCreateShopAsync(ownerUserId, ct);

        return await _db.ProductDiscounts.AsNoTracking()
            .Where(d => d.ProductSku.Product.ShopId == shop.Id)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DiscountDto(d.Id, d.ProductSkuId, d.ProductSku.VariantName, d.DiscountPercent, d.StartAt, d.EndAt, d.IsActive))
            .ToListAsync(ct);
    }

    private async Task<ProductSku> GetOwnedSkuAsync(long ownerUserId, long skuId, CancellationToken ct)
    {
        var sku = await _db.ProductSkus
            .Include(s => s.Product).ThenInclude(p => p.Shop)
            .Include(s => s.Discounts)
            .FirstOrDefaultAsync(s => s.Id == skuId, ct)
            ?? throw new NotFoundException("SKU", skuId);

        if (sku.Product.Shop.OwnerUserId != ownerUserId)
        {
            throw new ForbiddenException("SKU không thuộc shop của bạn.");
        }

        return sku;
    }

    private static ProductDetailDto MapDetail(Product product)
    {
        var now = DateTime.UtcNow;
        var skus = product.Skus.Select(s =>
        {
            var discountPercent = DiscountCalculator.GetActiveDiscountPercent(s.Discounts, now);
            var effectivePrice = DiscountCalculator.ApplyDiscount(s.Price, discountPercent);
            return new SkuDto(s.Id, s.SkuCode, s.VariantName, s.Price, effectivePrice, discountPercent, s.StockQuantity);
        }).ToList();

        return new ProductDetailDto(
            product.Id, product.ShopId, product.Shop.Name, product.CategoryId,
            product.Category?.Name ?? string.Empty, product.Name, product.Description,
            product.IsPublic, product.CreatedAt, skus);
    }
}
