using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopeeMini.Application.Features.Auth;
using ShopeeMini.Application.Features.Auth.Dtos;

namespace ShopeeMini.API.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequestDto request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ClientIp, ct);
        return SuccessResponse(result, "Đăng ký thành công", StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ClientIp, ct);
        return SuccessResponse(result, "Đăng nhập thành công");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshTokenRequestDto request, CancellationToken ct)
    {
        var result = await _authService.RefreshAsync(request.RefreshToken, ClientIp, ct);
        return SuccessResponse(result, "Làm mới token thành công");
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(RefreshTokenRequestDto request, CancellationToken ct)
    {
        await _authService.LogoutAsync(request.RefreshToken, ct);
        return SuccessResponse("Đăng xuất thành công");
    }
}
