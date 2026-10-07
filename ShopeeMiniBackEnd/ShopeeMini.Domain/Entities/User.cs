using ShopeeMini.Domain.Common;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;

    public Shop? Shop { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
