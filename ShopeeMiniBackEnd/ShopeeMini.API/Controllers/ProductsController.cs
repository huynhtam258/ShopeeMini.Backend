using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopeeMini.Application.Features.Products;
using ShopeeMini.Application.Features.Products.Dtos;

namespace ShopeeMini.API.Controllers;

[Route("api/products")]
[AllowAnonymous]
public class ProductsController : ApiControllerBase
{
    private readonly IPublicCatalogService _catalogService;

    public ProductsController(IPublicCatalogService catalogService) => _catalogService = catalogService;

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] ProductQuery query, CancellationToken ct)
    {
        var result = await _catalogService.GetProductsAsync(query, ct);
        return SuccessResponse(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetProductDetail(long id, CancellationToken ct)
    {
        var result = await _catalogService.GetProductDetailAsync(id, ct);
        return SuccessResponse(result);
    }
}
