using Microsoft.EntityFrameworkCore;
using ShopeeMini.Application.Interfaces;
using ShopeeMini.Domain.Entities;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher)
    {
        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            var admin = new User
            {
                Email = "admin@shopeemini.local",
                PasswordHash = hasher.Hash("Admin@123"),
                FullName = "Shop Administrator",
                Role = UserRole.Admin,
                IsActive = true
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            db.Shops.Add(new Shop { OwnerUserId = admin.Id, Name = "ShopeeMini Official Store" });
        }

        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "Điện thoại & Phụ kiện" },
                new Category { Name = "Thời trang" },
                new Category { Name = "Đồ gia dụng" },
                new Category { Name = "Sách & Văn phòng phẩm" });
        }

        await db.SaveChangesAsync();
    }
}
