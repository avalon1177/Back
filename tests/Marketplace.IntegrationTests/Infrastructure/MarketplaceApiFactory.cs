using System.Data.Common;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Marketplace.IntegrationTests.Infrastructure;

public sealed class MarketplaceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public FakeEmailService EmailService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Data Source=marketplace-tests",
                ["Jwt:Issuer"] = "Marketplace.Tests",
                ["Jwt:Audience"] = "Marketplace.Tests",
                ["Jwt:Key"] = "MarketplaceIntegrationTestsSecretKey1234567890",
                ["Jwt:ExpiresMinutes"] = "180",
                ["Frontend:BaseUrl"] = "https://frontend.test",
                ["AppBaseUrl"] = "https://api.test",
                ["Minio:Bucket"] = "tests",
                ["Smtp:Host"] = "localhost",
                ["Smtp:User"] = "tests@example.com",
                ["Smtp:Password"] = "password",
                ["LiqPay:PublicKey"] = "public",
                ["LiqPay:PrivateKey"] = "private"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IEmailService>();
            services.RemoveAll<IStorageService>();
            services.RemoveAll<ILiqPayService>();
            services.RemoveAll<ISmsService>();
            services.RemoveAll<IExternalAuthService>();
            services.RemoveAll<DbConnection>();

            services.AddSingleton<DbConnection>(_ => _connection);
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseSqlite(sp.GetRequiredService<DbConnection>());
            });

            services.AddSingleton(EmailService);
            services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<FakeEmailService>());
            services.AddSingleton<IStorageService, InMemoryStorageService>();
            services.AddSingleton<ILiqPayService, FakeLiqPayService>();
            services.AddSingleton<ISmsService, FakeSmsService>();
            services.AddSingleton<IExternalAuthService, FakeExternalAuthService>();
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await ResetStateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task ResetStateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        EmailService.Reset();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { UserRole.Admin, UserRole.Seller, UserRole.Buyer })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        const string adminEmail = "admin@integration.test";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new AppUser
            {
                Email = adminEmail,
                UserName = adminEmail,
                DisplayName = "Integration Admin",
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(admin, "Admin123!");
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException("Failed to seed integration-test admin user.");
            }
        }

        var roles = await userManager.GetRolesAsync(admin);
        if (!roles.Contains(UserRole.Admin))
        {
            foreach (var role in roles)
            {
                await userManager.RemoveFromRoleAsync(admin, role);
            }

            await userManager.AddToRoleAsync(admin, UserRole.Admin);
        }
    }
}
