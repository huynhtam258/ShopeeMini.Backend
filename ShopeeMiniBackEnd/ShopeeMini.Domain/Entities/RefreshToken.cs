using ShopeeMini.Domain.Common;

namespace ShopeeMini.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? CreatedByIp { get; set; }
    public string? ReplacedByToken { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
