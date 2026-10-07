using FluentValidation;
using ShopeeMini.Application.Features.Products.Dtos;

namespace ShopeeMini.Application.Features.Products.Validators;

public class CreateSkuDtoValidator : AbstractValidator<CreateSkuDto>
{
    public CreateSkuDtoValidator()
    {
        RuleFor(x => x.SkuCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.VariantName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
    }
}

public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Skus).NotEmpty().WithMessage("Sản phẩm phải có ít nhất 1 SKU.");
        RuleForEach(x => x.Skus).SetValidator(new CreateSkuDtoValidator());
    }
}

public class UpdateProductDtoValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductDtoValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
    }
}

public class UpdateStockDtoValidator : AbstractValidator<UpdateStockDto>
{
    public UpdateStockDtoValidator()
    {
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
    }
}

public class UpdateSkuPriceDtoValidator : AbstractValidator<UpdateSkuPriceDto>
{
    public UpdateSkuPriceDtoValidator()
    {
        RuleFor(x => x.Price).GreaterThan(0);
    }
}

public class CreateDiscountDtoValidator : AbstractValidator<CreateDiscountDto>
{
    public CreateDiscountDtoValidator()
    {
        RuleFor(x => x.ProductSkuId).GreaterThan(0);
        RuleFor(x => x.DiscountPercent).GreaterThan(0).LessThanOrEqualTo(90);
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt);
    }
}
