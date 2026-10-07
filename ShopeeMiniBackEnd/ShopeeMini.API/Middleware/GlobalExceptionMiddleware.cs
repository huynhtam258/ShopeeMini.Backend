using System.Net;
using System.Text.Json;
using FluentValidation;
using ShopeeMini.Application.Common.Exceptions;
using ShopeeMini.Application.Common.Models;

namespace ShopeeMini.API.Middleware;

/// <summary>Catches every unhandled exception once, at the edge, and maps it to the standard ApiResponse envelope + HTTP status code.</summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException fluentEx => (
                HttpStatusCode.BadRequest,
                ApiResponse.Fail("Dữ liệu đầu vào không hợp lệ.", fluentEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))),

            ValidationAppException validationEx => (
                HttpStatusCode.BadRequest,
                ApiResponse.Fail(validationEx.Message, validationEx.Errors)),

            NotFoundException notFoundEx => (HttpStatusCode.NotFound, ApiResponse.Fail(notFoundEx.Message)),
            BadRequestException badRequestEx => (HttpStatusCode.BadRequest, ApiResponse.Fail(badRequestEx.Message)),
            ConflictException conflictEx => (HttpStatusCode.Conflict, ApiResponse.Fail(conflictEx.Message)),
            ForbiddenException forbiddenEx => (HttpStatusCode.Forbidden, ApiResponse.Fail(forbiddenEx.Message)),
            UnauthorizedAppException unauthorizedEx => (HttpStatusCode.Unauthorized, ApiResponse.Fail(unauthorizedEx.Message)),

            _ => (HttpStatusCode.InternalServerError, ApiResponse.Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."))
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("{ExceptionType} while processing {Method} {Path}: {Message}",
                exception.GetType().Name, context.Request.Method, context.Request.Path, exception.Message);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
