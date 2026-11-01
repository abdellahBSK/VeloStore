namespace VeloStore.Application.Options;

/// <summary>
/// Cache lifetimes for the multi-layer cache (memory, then Redis, then SQL Server).
/// Bound from the <c>CacheSettings</c> configuration section.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "CacheSettings";

    /// <summary>Prefix added to every Redis key so several apps can share one Redis.</summary>
    public string RedisInstanceName { get; set; } = "VeloStore:";

    /// <summary>How long the home page product list stays in the in-process memory cache (L1).</summary>
    public int MemoryCacheDurationMinutes { get; set; } = 5;

    /// <summary>How long the home page product list stays in Redis (L2).</summary>
    public int HomePageCacheDurationMinutes { get; set; } = 10;

    /// <summary>How long a single product's details stay cached, in both layers.</summary>
    public int ProductDetailsCacheDurationMinutes { get; set; } = 30;

    /// <summary>Sliding expiration of a shopping cart stored in Redis.</summary>
    public int CartExpirationHours { get; set; } = 6;
}
