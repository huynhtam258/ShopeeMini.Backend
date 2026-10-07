using FluentValidation;
using ShopeeMini.Application.Features.Cart.Dtos;

namespace ShopeeMini.Application.Features.Cart.Validators;

public class AddCartItemDtoValidator : AbstractValidator<AddCartItemDto>
{
    public AddCartItemDtoValidator()
    {
        RuleFor(x => x.ProductSkuId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1000);
    }
}

public class UpdateCartItemDtoValidator : AbstractValidator<UpdateCartItemDto>
{
    public UpdateCartItemDtoValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1000);
    }
}
