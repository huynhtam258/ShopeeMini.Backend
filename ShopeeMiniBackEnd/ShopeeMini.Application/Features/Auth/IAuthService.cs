using ShopeeMini.Application.Features.Auth.Dtos;

namespace ShopeeMini.Application.Features.Auth;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterRequestDto request, string? ipAddress, CancellationToken ct = default);
    Task<AuthResultDto> LoginAsync(LoginRequestDto request, string? ipAddress, CancellationToken ct = default);
    Task<AuthResultDto> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<UserProfileDto> GetProfileAsync(long userId, CancellationToken ct = default);
    Task ChangePasswordAsync(long userId, ChangePasswordRequestDto request, CancellationToken ct = default);
}
