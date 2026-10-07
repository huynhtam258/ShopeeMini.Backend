using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShopeeMini.Application.Common;
using ShopeeMini.Application.Features.Auth;
using ShopeeMini.Application.Features.Cart;
using ShopeeMini.Application.Features.Categories;
using ShopeeMini.Application.Features.Notifications;
using ShopeeMini.Application.Features.Orders;
using ShopeeMini.Application.Features.Products;
using ShopeeMini.Application.Interfaces;

namespace ShopeeMini.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IPublicCatalogService, PublicCatalogService>();
        services.AddScoped<IAdminProductService, AdminProductService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAdminOrderService, AdminOrderService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();

        return services;
    }
}
