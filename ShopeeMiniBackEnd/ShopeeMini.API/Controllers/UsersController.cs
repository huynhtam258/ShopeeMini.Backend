using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopeeMini.Application.Features.Auth;
using ShopeeMini.Application.Features.Auth.Dtos;

namespace ShopeeMini.API.Controllers;

[Route("api/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public UsersController(IAuthService authService) => _authService = authService;

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var profile = await _authService.GetProfileAsync(CurrentUserId, ct);
        return SuccessResponse(profile);
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequestDto request, CancellationToken ct)
    {
        await _authService.ChangePasswordAsync(CurrentUserId, request, ct);
        return SuccessResponse("Đổi mật khẩu thành công");
    }
}
