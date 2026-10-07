using FluentValidation;
using ShopeeMini.Application.Features.Categories.Dtos;

namespace ShopeeMini.Application.Features.Categories.Validators;

public class UpsertCategoryValidator : AbstractValidator<UpsertCategoryDto>
{
    public UpsertCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}
