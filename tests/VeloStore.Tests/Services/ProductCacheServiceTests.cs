using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using VeloStore.Data;
using VeloStore.Services;
using VeloStore.Tests.TestDoubles;

namespace VeloStore.Tests.Services;

public class ProductCacheServiceTests
{
    private static ProductCacheService CreateService(
        VeloStoreDbContext context,
        IMemoryCache? memoryCache = null,
        IDistributedCache? distributedCache = null) =>
        new(context,
            memoryCache ?? TestData.NewMemoryCache(),
            distributedCache ?? TestData.NewDistributedCache(),
            TestData.CacheOptions,
            NullLogger<ProductCacheService>.Instance);

    private static void DeleteAllProducts(VeloStoreDbContext context)
    {
        context.Products.RemoveRange(context.Products);
        context.SaveChanges();
    }

    [Fact]
    public async Task Home_products_are_loaded_from_the_database_on_a_cold_cache()
    {
        using var context = TestData.NewSeededContext();

        var products = await CreateService(context).GetHomeProductsAsync();

        Assert.Equal(4, products.Count);
    }

    [Fact]
    public async Task Second_call_is_served_from_the_memory_cache()
    {
        using var context = TestData.NewSeededContext();
        var service = CreateService(context);
        await service.GetHomeProductsAsync();

        DeleteAllProducts(context);
        var products = await service.GetHomeProductsAsync();

        Assert.Equal(4, products.Count);
    }

    [Fact]
    public async Task Memory_cache_miss_falls_back_to_redis_before_the_database()
    {
        using var context = TestData.NewSeededContext();
        var sharedRedis = TestData.NewDistributedCache();
        await CreateService(context, distributedCache: sharedRedis).GetHomeProductsAsync();

        // Another server instance: empty memory cache, same Redis, database no longer has the rows.
        DeleteAllProducts(context);
        var products = await CreateService(context, distributedCache: sharedRedis).GetHomeProductsAsync();

        Assert.Equal(4, products.Count);
    }

    [Fact]
    public async Task Invalidating_the_cache_makes_the_next_call_read_the_database()
    {
        using var context = TestData.NewSeededContext();
        var service = CreateService(context);
        await service.GetHomeProductsAsync();

        DeleteAllProducts(context);
        await service.InvalidateHomeProductsCacheAsync();

        Assert.Empty(await service.GetHomeProductsAsync());
    }

    [Fact]
    public async Task Redis_outage_does_not_break_the_home_page()
    {
        using var context = TestData.NewSeededContext();

        var products = await CreateService(context, distributedCache: new ThrowingDistributedCache()).GetHomeProductsAsync();

        Assert.Equal(4, products.Count);
    }

    [Fact]
    public async Task Product_details_are_cached_and_unknown_ids_return_null()
    {
        using var context = TestData.NewSeededContext();
        var service = CreateService(context);
        var id = context.Products.Single(p => p.Name == "Aero Helmet V2").Id;

        var first = await service.GetProductDetailsAsync(id);
        DeleteAllProducts(context);
        var cached = await service.GetProductDetailsAsync(id);

        Assert.NotNull(first);
        Assert.Equal("Aero Helmet V2", cached?.Name);
        Assert.Null(await service.GetProductDetailsAsync(9999));
    }

    [Fact]
    public async Task Filter_by_text_matches_name_or_description()
    {
        using var context = TestData.NewSeededContext();

        var products = await CreateService(context).GetFilteredProductsAsync("carbon", null, null, null);

        Assert.Equal("Speed Demon XR", Assert.Single(products).Name);
    }

    [Fact]
    public async Task Filter_by_price_range_and_sort_by_price()
    {
        using var context = TestData.NewSeededContext();

        var products = await CreateService(context).GetFilteredProductsAsync(null, 100m, 2000m, "price_desc");

        Assert.Equal(new[] { "Trail Blazer 5000", "Aero Helmet V2" }, products.Select(p => p.Name));
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        private static Exception Down() => new InvalidOperationException("Redis is down");

        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Down();
    }
}
