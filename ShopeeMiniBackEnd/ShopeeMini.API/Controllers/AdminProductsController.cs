using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopeeMini.Application.Features.Products;
using ShopeeMini.Application.Features.Products.Dtos;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.API.Controllers;

[Route("api/admin/products")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminProductsController : ApiControllerBase
{
    private readonly IAdminProductService _productService;

    public AdminProductsController(IAdminProductService productService) => _productService = productService;

    [HttpGet]
    public async Task<IActionResult> GetMyProducts([FromQuery] ProductQuery query, CancellationToken ct)
    {
        var result = await _productService.GetMyProductsAsync(CurrentUserId, query, ct);
        return SuccessResponse(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetProduct(long id, CancellationToken ct)
    {
        var result = await _productService.GetProductForAdminAsync(CurrentUserId, id, ct);
        return SuccessResponse(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProductDto request, CancellationToken ct)
    {
        var result = await _productService.CreateProductAsync(CurrentUserId, request, ct);
        return SuccessResponse(result, "Tạo sản phẩm thành công", StatusCodes.Status201Created);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateProductDto request, CancellationToken ct)
    {
        var result = await _productService.UpdateProductAsync(CurrentUserId, id, request, ct);
        return SuccessResponse(result, "Cập nhật sản phẩm thành công");
    }

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, UpdateProductStatusDto request, CancellationToken ct)
    {
        await _productService.SetProductStatusAsync(CurrentUserId, id, request.IsPublic, ct);
        var message = request.IsPublic ? "Đã hiển thị sản phẩm" : "Đã ẩn sản phẩm";
        return SuccessResponse(message);
    }

    [HttpPost("{id:long}/skus")]
    public async Task<IActionResult> AddSku(long id, CreateSkuDto request, CancellationToken ct)
    {
        var result = await _productService.AddSkuAsync(CurrentUserId, id, request, ct);
        return SuccessResponse(result, "Thêm SKU thành công", StatusCodes.Status201Created);
    }

    [HttpPut("skus/{skuId:long}/stock")]
    public async Task<IActionResult> UpdateStock(long skuId, UpdateStockDto request, CancellationToken ct)
    {
        var result = await _productService.UpdateSkuStockAsync(CurrentUserId, skuId, request, ct);
        return SuccessResponse(result, "Cập nhật tồn kho thành công");
    }

    [HttpPut("skus/{skuId:long}/price")]
    public async Task<IActionResult> UpdatePrice(long skuId, UpdateSkuPriceDto request, CancellationToken ct)
    {
        var result = await _productService.UpdateSkuPriceAsync(CurrentUserId, skuId, request, ct);
        return SuccessResponse(result, "Cập nhật giá thành công");
    }
}

[Route("api/admin/discounts")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminDiscountsController : ApiControllerBase
{
    private readonly IAdminProductService _productService;

    public AdminDiscountsController(IAdminProductService productService) => _productService = productService;

    [HttpGet]
    public async Task<IActionResult> GetMyDiscounts(CancellationToken ct)
    {
        var result = await _productService.GetMyDiscountsAsync(CurrentUserId, ct);
        return SuccessResponse(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDiscountDto request, CancellationToken ct)
    {
        var result = await _productService.CreateDiscountAsync(CurrentUserId, request, ct);
        return SuccessResponse(result, "Tạo khuyến mãi thành công", StatusCodes.Status201Created);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Deactivate(long id, CancellationToken ct)
    {
        await _productService.DeactivateDiscountAsync(CurrentUserId, id, ct);
        return SuccessResponse("Đã hủy khuyến mãi");
    }
}
