using Microsoft.AspNetCore.Mvc;
using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Interfaces;

namespace ShopeeMini.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ICurrentUserService? _currentUser;

    protected ICurrentUserService CurrentUser =>
        _currentUser ??= HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

    protected long CurrentUserId => CurrentUser.UserId!.Value;

    protected static ObjectResult SuccessResponse<T>(T data, string message = "Thao tác thành công", int statusCode = StatusCodes.Status200OK) =>
        new(ApiResponse<T>.Ok(data, message)) { StatusCode = statusCode };

    protected static ObjectResult SuccessResponse(string message = "Thao tác thành công") =>
        new(ApiResponse.Ok(message)) { StatusCode = StatusCodes.Status200OK };
}
