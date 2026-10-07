using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Features.Products.Dtos;

namespace ShopeeMini.Application.Features.Products;

public interface IAdminProductService
{
    Task<PagedResult<ProductListItemDto>> GetMyProductsAsync(long ownerUserId, ProductQuery query, CancellationToken ct = default);
    Task<ProductDetailDto> GetProductForAdminAsync(long ownerUserId, long productId, CancellationToken ct = default);
    Task<ProductDetailDto> CreateProductAsync(long ownerUserId, CreateProductDto request, CancellationToken ct = default);
    Task<ProductDetailDto> UpdateProductAsync(long ownerUserId, long productId, UpdateProductDto request, CancellationToken ct = default);

    /// <summary>Toggles a product's visibility. Hiding it atomically strips it from every user's cart and notifies them.</summary>
    Task SetProductStatusAsync(long ownerUserId, long productId, bool isPublic, CancellationToken ct = default);

    Task<SkuDto> AddSkuAsync(long ownerUserId, long productId, CreateSkuDto request, CancellationToken ct = default);
    Task<SkuDto> UpdateSkuStockAsync(long ownerUserId, long skuId, UpdateStockDto request, CancellationToken ct = default);
    Task<SkuDto> UpdateSkuPriceAsync(long ownerUserId, long skuId, UpdateSkuPriceDto request, CancellationToken ct = default);

    Task<DiscountDto> CreateDiscountAsync(long ownerUserId, CreateDiscountDto request, CancellationToken ct = default);
    Task DeactivateDiscountAsync(long ownerUserId, long discountId, CancellationToken ct = default);
    Task<IReadOnlyList<DiscountDto>> GetMyDiscountsAsync(long ownerUserId, CancellationToken ct = default);
}
