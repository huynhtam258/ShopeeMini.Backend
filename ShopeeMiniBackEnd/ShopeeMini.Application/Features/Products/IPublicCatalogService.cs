using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Features.Products.Dtos;

namespace ShopeeMini.Application.Features.Products;

public interface IPublicCatalogService
{
    Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductQuery query, CancellationToken ct = default);
    Task<ProductDetailDto> GetProductDetailAsync(long productId, CancellationToken ct = default);
}
