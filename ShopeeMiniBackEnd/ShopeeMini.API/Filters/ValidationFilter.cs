using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ShopeeMini.API.Filters;

/// <summary>
/// Runs FluentValidation against every action argument that has a registered validator, before
/// the action body executes. Centralizes rule #3 ("always validate the input DTO with
/// FluentValidation before running any logic") so controllers don't repeat this boilerplate.
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationFilter(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_serviceProvider.GetService(validatorType) is IValidator validator)
            {
                var validationContext = new ValidationContext<object>(argument);
                var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
                if (!result.IsValid)
                {
                    throw new ValidationException(result.Errors);
                }
            }
        }

        await next();
    }
}
