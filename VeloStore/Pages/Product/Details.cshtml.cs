using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using VeloStore.Services;

namespace VeloStore.Pages.Product
{
    /// <summary>
    /// Product details page with caching support
    /// </summary>
    public class DetailsModel : PageModel
    {
        private readonly IProductCacheService _productCacheService;
        private readonly ICartService _cartService;
        private readonly ILogger<DetailsModel> _logger;

        public ViewModels.ProductDetailsVM? Product { get; set; }

        public DetailsModel(
            IProductCacheService productCacheService,
            ICartService cartService,
            ILogger<DetailsModel> logger)
        {
            _productCacheService = productCacheService;
            _cartService = cartService;
            _logger = logger;
        }

        /// <summary>
        /// GET: Display product details with caching
        /// </summary>
        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid product ID: {ProductId}", id);
                return RedirectToPage("/Index");
            }

            try
            {
                Product = await _productCacheService.GetProductDetailsAsync(id);

                if (Product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found", id);
                    return RedirectToPage("/Index");
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product {ProductId}", id);
                return RedirectToPage("/Index");
            }
        }

        /// <summary>
        /// POST: Add product to cart (requires authentication)
        /// </summary>
        public async Task<IActionResult> OnPostAddToCartAsync(int productId)
        {
            if (productId <= 0)
            {
                _logger.LogWarning("Invalid product ID for cart: {ProductId}", productId);
                return RedirectToPage("/Index");
            }

            try
            {
                var product = await _productCacheService.GetProductDetailsAsync(productId);

                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found when adding to cart", productId);
                    return RedirectToPage("/Index");
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
