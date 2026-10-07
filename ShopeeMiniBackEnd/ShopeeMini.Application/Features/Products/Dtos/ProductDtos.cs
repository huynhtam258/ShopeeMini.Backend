using ShopeeMini.Application.Common.Models;

namespace ShopeeMini.Application.Features.Products.Dtos;

public record ProductQuery : PaginationQuery
{
    public long? CategoryId { get; set; }
    public string? Search { get; set; }
}

public record SkuDto(
    long Id,
    string SkuCode,
    string VariantName,
    decimal Price,
    decimal EffectivePrice,
    decimal ActiveDiscountPercent,
    int StockQuantity);

public record ProductListItemDto(
    long Id,
    string Name,
    string CategoryName,
    bool IsPublic,
    decimal MinPrice,
    decimal MaxEffectivePrice,
    decimal MinEffectivePrice,
    bool HasActiveDiscount,
    DateTime CreatedAt);

public record ProductDetailDto(
    long Id,
    long ShopId,
    string ShopName,
    long CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    bool IsPublic,
    DateTime CreatedAt,
    IReadOnlyList<SkuDto> Skus);

public record CreateSkuDto(string SkuCode, string VariantName, decimal Price, int StockQuantity);

public record CreateProductDto(long CategoryId, string Name, string? Description, List<CreateSkuDto> Skus);

public record UpdateProductDto(long CategoryId, string Name, string? Description);

public record UpdateProductStatusDto(bool IsPublic);

public record UpdateStockDto(int StockQuantity);

public record UpdateSkuPriceDto(decimal Price);

public record CreateDiscountDto(long ProductSkuId, decimal DiscountPercent, DateTime StartAt, DateTime EndAt);

public record DiscountDto(long Id, long ProductSkuId, string SkuVariantName, decimal DiscountPercent, DateTime StartAt, DateTime EndAt, bool IsActive);
