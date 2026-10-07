using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ShopeeMini.Application.Common.Exceptions;
using ShopeeMini.Application.Interfaces;

namespace ShopeeMini.API.Filters;

/// <summary>
/// Requires an "Idempotency-Key" header on the decorated action. The first request with a given
/// key executes normally and its response is cached; every subsequent request with the same key
/// (same user, same route) replays that cached response instead of re-running the action body.
/// Without this, a client that retries a checkout call after a network timeout — not knowing
/// whether the first attempt actually landed — would risk paying for / decrementing stock for
/// the same cart twice.
/// </summary>
public class IdempotencyFilter : IAsyncActionFilter
{
    private const string HeaderName = "Idempotency-Key";
    private readonly IIdempotencyService _idempotencyService;

    public IdempotencyFilter(IIdempotencyService idempotencyService) => _idempotencyService = idempotencyService;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues) || string.IsNullOrWhiteSpace(keyValues))
        {
            throw new BadRequestException($"Thiếu header '{HeaderName}' bắt buộc cho thao tác này.");
        }

        var key = keyValues.ToString();
        var path = context.HttpContext.Request.Path.ToString();
        var userIdRaw = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdRaw, out var userId))
        {
            throw new UnauthorizedAppException();
        }

        var ct = context.HttpContext.RequestAborted;
        var cached = await _idempotencyService.TryGetAsync(userId, key, path, ct);
        if (cached is not null)
        {
            context.Result = new ContentResult
            {
                StatusCode = cached.StatusCode,
                Content = cached.Body,
                ContentType = "application/json"
            };
            return;
        }

        var executedContext = await next();

        if (executedContext.Exception is null && executedContext.Result is ObjectResult { StatusCode: null or >= 200 and < 300 } objectResult)
        {
            var statusCode = objectResult.StatusCode ?? 200;
            var body = JsonSerializer.Serialize(objectResult.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await _idempotencyService.SaveAsync(userId, key, path, statusCode, body, ct);
        }
    }
}
