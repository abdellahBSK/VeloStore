using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VeloStore.Data;
using VeloStore.ViewModels;

namespace VeloStore.Services
{
    /// <summary>
    /// Professional multi-layer caching service for products
    /// Strategy: L1 (Memory Cache) -> L2 (Redis) -> L3 (Database)
    /// </summary>
    public class ProductCacheService : IProductCacheService
    {
        private readonly VeloStoreDbContext _context;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<ProductCacheService> _logger;

        // Cache keys
        private const string HOME_PRODUCTS_MEMORY_KEY = "home_products_memory";
        private const string HOME_PRODUCTS_REDIS_KEY = "home_products_redis";
        private const string PRODUCT_DETAILS_KEY_PREFIX = "product_details:";

        // Cache durations
        private readonly TimeSpan _memoryCacheDuration = TimeSpan.FromMinutes(5); // Short-lived for freshness
        private readonly TimeSpan _redisCacheDuration = TimeSpan.FromMinutes(10); // Longer for distributed cache
        private readonly TimeSpan _productDetailsCacheDuration = TimeSpan.FromMinutes(30);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ProductCacheService(
            VeloStoreDbContext context,
            IMemoryCache memoryCache,
            IDistributedCache distributedCache,
            ILogger<ProductCacheService> logger)
        {
            _context = context;
            _memoryCache = memoryCache;
            _distributedCache = distributedCache;
            _logger = logger;
        }

        /// <summary>
        /// Multi-layer cache strategy for home page products
        /// </summary>
        public async Task<List<HomeProductVM>> GetHomeProductsAsync()
        {
            try
            {
                // ============================================
                // LAYER 1: In-Memory Cache (Fastest - Per Server)
                // ============================================
                if (_memoryCache.TryGetValue(HOME_PRODUCTS_MEMORY_KEY, out List<HomeProductVM>? cachedProducts)
                    && cachedProducts != null)
                {
                    _logger.LogDebug("Home products retrieved from memory cache");
                    return cachedProducts;
                }

                // ============================================
                // LAYER 2: Redis Distributed Cache (Shared Across Servers)
                // ============================================
                try
                {
                    var redisData = await _distributedCache.GetStringAsync(HOME_PRODUCTS_REDIS_KEY);
                    if (!string.IsNullOrEmpty(redisData))
                    {
                        var products = JsonSerializer.Deserialize<List<HomeProductVM>>(redisData, _jsonOptions);
                        if (products != null && products.Any())
                        {
                            // Populate L1 cache from L2
                            _memoryCache.Set(HOME_PRODUCTS_MEMORY_KEY, products, _memoryCacheDuration);
                            _logger.LogDebug("Home products retrieved from Redis cache and stored in memory");
                            return products;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve from Redis cache, falling back to database");
                    // Continue to database fallback
                }

                // ============================================
                // LAYER 3: Database (Source of Truth)
                // ============================================
                _logger.LogInformation("Cache miss - Loading home products from database");
                var dbProducts = await _context.Products
                    .AsNoTracking()
                    .Select(p => new HomeProductVM
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Price = p.Price,
                        ImageUrl = p.ImageUrl
                    })
                    .ToListAsync();

                // Populate both cache layers
                await PopulateCachesAsync(dbProducts);

                return dbProducts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving home products");
                // Return empty list instead of crashing
                return new List<HomeProductVM>();
            }
        }

        /// <summary>
        /// Gets filtered products (no caching for filtered results to ensure accuracy)
        /// </summary>
        public async Task<List<HomeProductVM>> GetFilteredProductsAsync(
            string? query,
            decimal? minPrice,
            decimal? maxPrice,
            string? sort)
        {
            try
            {
                var productsQuery = _context.Products.AsQueryable();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(query))
                {
                    productsQuery = productsQuery.Where(p => 
                        p.Name.Contains(query) || 
                        (p.Description != null && p.Description.Contains(query)));
                }

                if (minPrice.HasValue)
                    productsQuery = productsQuery.Where(p => p.Price >= minPrice.Value);

                if (maxPrice.HasValue)
                    productsQuery = productsQuery.Where(p => p.Price <= maxPrice.Value);

                // Apply sorting
                productsQuery = sort switch
                {
                    "price_asc" => productsQuery.OrderBy(p => p.Price),
                    "price_desc" => productsQuery.OrderByDescending(p => p.Price),
                    "name_asc" => productsQuery.OrderBy(p => p.Name),
                    "name_desc" => productsQuery.OrderByDescending(p => p.Name),
                    _ => productsQuery.OrderBy(p => p.Id)
                };

                var products = await productsQuery
                    .AsNoTracking()
                    .Select(p => new HomeProductVM
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Price = p.Price,
                        ImageUrl = p.ImageUrl
                    })
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} filtered products from database", products.Count);
                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving filtered products");
                return new List<HomeProductVM>();
            }
        }

        /// <summary>
        /// Gets product details with caching
        /// </summary>
        public async Task<ProductDetailsVM?> GetProductDetailsAsync(int productId)
        {
            try
            {
                var cacheKey = $"{PRODUCT_DETAILS_KEY_PREFIX}{productId}";

                // Try memory cache first
                if (_memoryCache.TryGetValue(cacheKey, out ProductDetailsVM? cachedProduct)
                    && cachedProduct != null)
                {
                    _logger.LogDebug("Product {ProductId} retrieved from memory cache", productId);
                    return cachedProduct;
                }

                // Try Redis cache
                try
                {
                    var redisData = await _distributedCache.GetStringAsync(cacheKey);
                    if (!string.IsNullOrEmpty(redisData))
                    {
                        var product = JsonSerializer.Deserialize<ProductDetailsVM>(redisData, _jsonOptions);
                        if (product != null)
                        {
                            _memoryCache.Set(cacheKey, product, _productDetailsCacheDuration);
                            _logger.LogDebug("Product {ProductId} retrieved from Redis cache", productId);
                            return product;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve product {ProductId} from Redis", productId);
                }

                // Database fallback
                var dbProduct = await _context.Products
                    .AsNoTracking()
                    .Where(p => p.Id == productId)
                    .Select(p => new ProductDetailsVM
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        Price = p.Price,
                        Stock = p.Stock,
                        ImageUrl = p.ImageUrl
                    })
                    .FirstOrDefaultAsync();

                if (dbProduct != null)
                {
                    // Cache the result
                    _memoryCache.Set(cacheKey, dbProduct, _productDetailsCacheDuration);
                    
                    try
                    {
                        var options = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = _productDetailsCacheDuration
                        };
                        await _distributedCache.SetStringAsync(
                            cacheKey,
                            JsonSerializer.Serialize(dbProduct, _jsonOptions),
                            options);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache product {ProductId} in Redis", productId);
                    }
                }

                return dbProduct;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product details for product {ProductId}", productId);
                return null;
            }
        }

        /// <summary>
        /// Invalidates home products cache
        /// </summary>
        public async Task InvalidateHomeProductsCacheAsync()
        {
            try
            {
                _memoryCache.Remove(HOME_PRODUCTS_MEMORY_KEY);
                await _distributedCache.RemoveAsync(HOME_PRODUCTS_REDIS_KEY);
                _logger.LogInformation("Home products cache invalidated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating home products cache");
            }
        }

        /// <summary>
        /// Invalidates cache for a specific product
        /// </summary>
        public async Task InvalidateProductCacheAsync(int productId)
        {
            try
            {
                var cacheKey = $"{PRODUCT_DETAILS_KEY_PREFIX}{productId}";
                _memoryCache.Remove(cacheKey);
                await _distributedCache.RemoveAsync(cacheKey);
                
                // Also invalidate home products since it might contain this product
                await InvalidateHomeProductsCacheAsync();
                
                _logger.LogInformation("Product {ProductId} cache invalidated", productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating product {ProductId} cache", productId);
            }
        }

        /// <summary>
        /// Populates both memory and Redis caches
        /// </summary>
        private async Task PopulateCachesAsync(List<HomeProductVM> products)
        {
            // Populate memory cache (L1)
            _memoryCache.Set(HOME_PRODUCTS_MEMORY_KEY, products, _memoryCacheDuration);

            // Populate Redis cache (L2)
            try
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _redisCacheDuration
                };

                await _distributedCache.SetStringAsync(
                    HOME_PRODUCTS_REDIS_KEY,
                    JsonSerializer.Serialize(products, _jsonOptions),
                    options);

                _logger.LogDebug("Home products cached in both memory and Redis");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to populate Redis cache, memory cache still active");
                // Memory cache is still populated, so we can continue
            }
        }
    }
}

