using Microsoft.EntityFrameworkCore;
using ShopeeMini.Application.Common.Exceptions;
using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Features.Products.Dtos;
using ShopeeMini.Application.Interfaces;

namespace ShopeeMini.Application.Features.Products;

public class PublicCatalogService : IPublicCatalogService
{
    private readonly IAppDbContext _db;

    public PublicCatalogService(IAppDbContext db) => _db = db;

    public async Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductQuery query, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Read-only listing query: AsNoTracking + server-side projection (only the columns the
        // list view needs travel over the wire) + pagination pushed down to SQL via Skip/Take.
        var baseQuery = _db.Products.AsNoTracking()
            .Where(p => p.IsPublic)
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
                p.Skus.Min(s => s.Price),
                p.Skus.Max(s =>
                    s.Price * (1 - (s.Discounts
                        .Where(d => d.IsActive && d.StartAt <= now && d.EndAt >= now)
                        .Select(d => (decimal?)d.DiscountPercent)
                        .Max() ?? 0m) / 100m)),
                p.Skus.Min(s =>
                    s.Price * (1 - (s.Discounts
                        .Where(d => d.IsActive && d.StartAt <= now && d.EndAt >= now)
                        .Select(d => (decimal?)d.DiscountPercent)
                        .Max() ?? 0m) / 100m)),
                p.Skus.Any(s => s.Discounts.Any(d => d.IsActive && d.StartAt <= now && d.EndAt >= now)),
                p.CreatedAt)
            )
            .ToListAsync(ct);

        return new PagedResult<ProductListItemDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ProductDetailDto> GetProductDetailAsync(long productId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var product = await _db.Products.AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Category)
            .Include(p => p.Skus).ThenInclude(s => s.Discounts)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsPublic, ct)
            ?? throw new NotFoundException("Sản phẩm", productId);

        var skus = product.Skus.Select(s =>
        {
            var discountPercent = Common.DiscountCalculator.GetActiveDiscountPercent(s.Discounts, now);
            var effectivePrice = Common.DiscountCalculator.ApplyDiscount(s.Price, discountPercent);
            return new SkuDto(s.Id, s.SkuCode, s.VariantName, s.Price, effectivePrice, discountPercent, s.StockQuantity);
        }).ToList();

        return new ProductDetailDto(
            product.Id,
            product.ShopId,
            product.Shop.Name,
            product.CategoryId,
            product.Category.Name,
            product.Name,
            product.Description,
            product.IsPublic,
            product.CreatedAt,
            skus);
    }
}
