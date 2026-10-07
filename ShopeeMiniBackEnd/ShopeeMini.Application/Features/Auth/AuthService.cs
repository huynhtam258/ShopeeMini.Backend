using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopeeMini.Application.Common.Exceptions;
using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Features.Auth.Dtos;
using ShopeeMini.Application.Interfaces;
using ShopeeMini.Domain.Entities;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.Application.Features.Auth;

public class AuthService : IAuthService
{
    private readonly IAppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IPasswordHasher _hasher;
    private readonly JwtSettings _jwtSettings;

    public AuthService(IAppDbContext db, IJwtService jwt, IPasswordHasher hasher, IOptions<JwtSettings> jwtSettings)
    {
        _db = db;
        _jwt = jwt;
        _hasher = hasher;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterRequestDto request, string? ipAddress, CancellationToken ct = default)
    {
        var emailExists = await _db.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (emailExists)
        {
            throw new ConflictException("Email đã được sử dụng.");
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = _hasher.Hash(request.Password),
            FullName = request.FullName,
            Phone = request.Phone,
            Role = UserRole.User,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ipAddress, ct);
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequestDto request, string? ipAddress, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAppException("Email hoặc mật khẩu không đúng.");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException("Tài khoản đã bị khóa.");
        }

        return await IssueTokensAsync(user, ipAddress, ct);
    }

    public async Task<AuthResultDto> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);

        if (existing is null || !existing.IsActive)
        {
            throw new UnauthorizedAppException("Refresh token không hợp lệ hoặc đã hết hạn.");
        }

        existing.RevokedAt = DateTime.UtcNow;

        var result = await IssueTokensAsync(existing.User, ipAddress, ct, replacesToken: existing);
        return result;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);
        if (existing is not null && existing.IsActive)
        {
            existing.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserProfileDto> GetProfileAsync(long userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Người dùng", userId);

        return new UserProfileDto(user.Id, user.Email, user.FullName, user.Phone, user.Role.ToString(), user.CreatedAt);
    }

    public async Task ChangePasswordAsync(long userId, ChangePasswordRequestDto request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Người dùng", userId);

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new BadRequestException("Mật khẩu hiện tại không đúng.");
        }

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<AuthResultDto> IssueTokensAsync(User user, string? ipAddress, CancellationToken ct, RefreshToken? replacesToken = null)
    {
        var accessToken = _jwt.GenerateAccessToken(user, out var accessExpiresAt);
        var refreshTokenValue = _jwt.GenerateRefreshToken();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = refreshExpiresAt,
            CreatedByIp = ipAddress
        };

        if (replacesToken is not null)
        {
            replacesToken.ReplacedByToken = refreshTokenValue;
        }

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync(ct);

        return new AuthResultDto(
            user.Id,
            user.Email,
            user.FullName,
            user.Role.ToString(),
            accessToken,
            accessExpiresAt,
            refreshTokenValue,
            refreshExpiresAt);
    }
}
