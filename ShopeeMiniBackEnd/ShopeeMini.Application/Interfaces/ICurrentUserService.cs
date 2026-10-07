using ShopeeMini.Domain.Enums;

namespace ShopeeMini.Application.Interfaces;

public interface ICurrentUserService
{
    long? UserId { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
}
