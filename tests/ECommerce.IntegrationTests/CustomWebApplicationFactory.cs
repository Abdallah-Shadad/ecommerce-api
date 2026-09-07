using ECommerce.Domain.Entities.Catalog;
using ECommerce.Domain.Entities.Identity;
using ECommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "ECommerceIntegrationTestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing AppDbContext and DbContextOptions registrations
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(AppDbContext)).ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            // Create an isolated internal service provider for InMemory database
            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Ensure test product exists for integration test
        if (!await db.Products.AnyAsync(p => p.Id == 1))
        {
            var category = new Category
            {
                Id = 1,
                Name = "Electronics",
                Description = "Electronic items and gadgets"
            };
            db.Categories.Add(category);

            var product = new Product
            {
                Id = 1,
                Name = "Gaming Mouse",
                Slug = "gaming-mouse",
                Description = "High precision optical gaming mouse",
                Price = 60m,
                StockQuantity = 20,
                SKU = "GM-100",
                CategoryId = 1,
                IsActive = true
            };
            db.Products.Add(product);

            await db.SaveChangesAsync();
        }
    }
}
