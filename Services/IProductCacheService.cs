using VeloStore.ViewModels;

namespace VeloStore.Services
{
    /// <summary>
    /// Service interface for caching product data with multi-layer strategy
    /// </summary>
    public interface IProductCacheService
    {
        /// <summary>
        /// Gets all products for home page with multi-layer caching (Memory -> Redis -> DB)
        /// </summary>
        Task<List<HomeProductVM>> GetHomeProductsAsync();

        /// <summary>
        /// Gets filtered products with caching support
        /// </summary>
        Task<List<HomeProductVM>> GetFilteredProductsAsync(
            string? query,
            decimal? minPrice,
            decimal? maxPrice,
            string? sort);

        /// <summary>
        /// Invalidates the home products cache (call when products are added/updated/deleted)
        /// </summary>
        Task InvalidateHomeProductsCacheAsync();

        /// <summary>
        /// Gets a single product by ID with caching
        /// </summary>
        Task<ProductDetailsVM?> GetProductDetailsAsync(int productId);

        /// <summary>
        /// Invalidates product cache for a specific product
        /// </summary>
        Task InvalidateProductCacheAsync(int productId);
    }
}

