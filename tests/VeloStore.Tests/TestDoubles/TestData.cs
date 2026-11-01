using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VeloStore.Application.Options;
using VeloStore.Data;
using VeloStore.Models;

namespace VeloStore.Tests.TestDoubles;

public static class TestData
{
    public static IOptions<CacheOptions> CacheOptions { get; } = Options.Create(new CacheOptions());

    public static IDistributedCache NewDistributedCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    public static IMemoryCache NewMemoryCache() => new MemoryCache(new MemoryCacheOptions());

    /// <summary>A fresh in-memory database seeded with a small catalog.</summary>
    public static VeloStoreDbContext NewSeededContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<VeloStoreDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new VeloStoreDbContext(options);
        if (!context.Products.Any())
        {
            var bikes = new Category { Name = "Bikes" };
            var accessories = new Category { Name = "Accessories" };
            context.Categories.AddRange(bikes, accessories);
            context.Products.AddRange(
                MakeProduct("Trail Blazer 5000", "A rugged mountain bike.", 1299.99m, 15, bikes),
                MakeProduct("Speed Demon XR", "Lightweight carbon road bike.", 2499.99m, 8, bikes),
                MakeProduct("Aero Helmet V2", "Aerodynamic helmet for racers.", 149.99m, 50, accessories),
                MakeProduct("Premium Padded Gloves", "Comfortable gloves for long rides.", 29.99m, 0, accessories));
            context.SaveChanges();
        }

        return context;
    }

    private static Product MakeProduct(string name, string description, decimal price, int stock, Category category) => new()
    {
        Name = name,
        Description = description,
        Price = price,
        Stock = stock,
        ImageUrl = $"/images/products/{name.ToLowerInvariant().Replace(' ', '-')}.jpg",
        Category = category
    };
}
