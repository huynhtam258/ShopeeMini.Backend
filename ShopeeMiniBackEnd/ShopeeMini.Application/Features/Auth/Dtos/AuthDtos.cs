namespace ShopeeMini.Application.Features.Auth.Dtos;

public record RegisterRequestDto(string Email, string Password, string FullName, string? Phone);

public record LoginRequestDto(string Email, string Password);

public record RefreshTokenRequestDto(string RefreshToken);

public record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);

public record AuthResultDto(
    long UserId,
    string Email,
    string FullName,
    string Role,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

public record UserProfileDto(
    long Id,
    string Email,
    string FullName,
    string? Phone,
    string Role,
    DateTime CreatedAt);
