using ShopeeMini.Domain.Entities;

namespace ShopeeMini.Application.Interfaces;

public record TokenPair(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt);

public interface IJwtService
{
    string GenerateAccessToken(User user, out DateTime expiresAt);
    string GenerateRefreshToken();
}
