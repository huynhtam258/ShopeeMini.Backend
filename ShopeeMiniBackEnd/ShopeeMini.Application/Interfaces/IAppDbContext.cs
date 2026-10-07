using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ShopeeMini.Domain.Entities;

namespace ShopeeMini.Application.Interfaces;

/// <summary>
/// Abstraction over the EF Core DbContext exposed to the Application layer, so use-case
/// services depend only on this interface (Dependency Inversion) and not on Infrastructure/EF Core directly.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Shop> Shops { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductSku> ProductSkus { get; }
    DbSet<ProductDiscount> ProductDiscounts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
