using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopeeMini.Application.Common.Models;
using ShopeeMini.Application.Interfaces;
using ShopeeMini.Infrastructure.Auth;
using ShopeeMini.Infrastructure.Files;
using ShopeeMini.Infrastructure.Persistence;

namespace ShopeeMini.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing 'ConnectionStrings:Default' configuration.");

        // A fixed ServerVersion avoids EF Core opening a connection just to probe the server
        // version at startup (which would also break `dotnet ef migrations add` when no live
        // MySQL instance is reachable). Bump this if you target a different MySQL release.
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(connectionString, serverVersion));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IInvoiceJsonWriter, InvoiceJsonWriter>();

        return services;
    }
}
