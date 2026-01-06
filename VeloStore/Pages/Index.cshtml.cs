using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using VeloStore.Services;
using VeloStore.ViewModels;

namespace VeloStore.Pages
{
    /// <summary>
    /// Home page with professional multi-layer caching strategy
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly IProductCacheService _productCacheService;
        private readonly ICartService _cartService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IProductCacheService productCacheService,
            ICartService cartService,
            ILogger<IndexModel> logger)
        {
            _productCacheService = productCacheService;
            _cartService = cartService;
            _logger = logger;
        }

        public List<HomeProductVM> Products { get; set; } = new();
        public SearchVM Search { get; set; } = new();

        /// <summary>
        /// GET: Home page with search and filtering
        /// Uses multi-layer caching for unfiltered results
        /// </summary>
        public async Task OnGetAsync(
            string? query,
            decimal? minPrice,
            decimal? maxPrice,
            string? sort)
        {
            try
            {
                Search = new SearchVM
                {
                    Query = query,
                    MinPrice = minPrice,
                    MaxPrice = maxPrice,
                    Sort = sort
                };

                bool hasFilters =
                    !string.IsNullOrWhiteSpace(query) ||
                    minPrice.HasValue ||
                    maxPrice.HasValue ||
                    !string.IsNullOrWhiteSpace(sort);

                // Use cached products if no filters (multi-layer cache: Memory -> Redis -> DB)
                if (!hasFilters)
                {
                    Products = await _productCacheService.GetHomeProductsAsync();
                }
                else
                {
                    // For filtered results, query database directly (ensures accuracy)
                    Products = await _productCacheService.GetFilteredProductsAsync(
                        query,
                        minPrice,
                        maxPrice,
                        sort);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products on home page");
                Products = new List<HomeProductVM>();
                TempData["ErrorMessage"] = "An error occurred while loading products. Please try again.";
            }
        }

        /// <summary>
        /// POST: Add product to cart (works for both authenticated and guest users)
        /// </summary>
        public async Task<IActionResult> OnPostAddToCartAsync(int productId)
        {
            if (productId <= 0)
            {
                _logger.LogWarning("Invalid product ID for cart: {ProductId}", productId);
                return RedirectToPage();
            }

            try
            {
                // Get product from cache
                var product = await _productCacheService.GetProductDetailsAsync(productId);

                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found when adding to cart", productId);
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToPage();
                }

                // Check stock availability
                if (product.Stock <= 0)
                {
                    TempData["ErrorMessage"] = "This product is out of stock.";
                    return RedirectToPage();
                }

                await _cartService.AddToCartAsync(
                    product.Id,
                    product.Name,
                    product.Price,
                    product.ImageUrl);

                TempData["SuccessMessage"] = $"{product.Name} added to cart successfully!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding product {ProductId} to cart", productId);
                TempData["ErrorMessage"] = "An error occurred while adding the product to cart.";
                return RedirectToPage();
            }
        }
    }
}
