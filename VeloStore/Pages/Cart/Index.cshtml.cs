using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using VeloStore.Services;
using VeloStore.ViewModels;

namespace VeloStore.Pages.Cart
{
    /// <summary>
    /// Shopping cart page - each user has their own cart stored in Redis
    /// </summary>
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ICartService _cartService;
        private readonly ILogger<IndexModel> _logger;

        /// <summary>
        /// Cart items displayed in the view
        /// </summary>
        public List<CartItemVM> Items { get; private set; } = new();

        /// <summary>
        /// Total cart amount
        /// </summary>
        public decimal Total { get; private set; }

        public IndexModel(ICartService cartService, ILogger<IndexModel> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        /// <summary>
        /// GET: Display cart
        /// </summary>
        public async Task OnGetAsync()
        {
            try
            {
                var cart = await _cartService.GetCartAsync();

                Items = cart.Items.Select(i => new CartItemVM
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Price = i.Price,
                    ImageUrl = i.ImageUrl,
                    Quantity = i.Quantity
                }).ToList();

                Total = Items.Sum(i => i.Price * i.Quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading cart");
                Items = new List<CartItemVM>();
                Total = 0;
                TempData["ErrorMessage"] = "An error occurred while loading your cart.";
            }
        }

        /// <summary>
        /// POST: Increase product quantity
        /// </summary>
        public async Task<IActionResult> OnPostIncreaseAsync(int productId)
        {
            if (productId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid product.";
                return RedirectToPage();
            }

            try
            {
                await _cartService.IncreaseAsync(productId);
                TempData["SuccessMessage"] = "Quantity updated.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error increasing quantity for product {ProductId}", productId);
                TempData["ErrorMessage"] = "An error occurred while updating quantity.";
            }

            return RedirectToPage();
        }

        /// <summary>
        /// POST: Decrease product quantity
        /// </summary>
        public async Task<IActionResult> OnPostDecreaseAsync(int productId)
        {
            if (productId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid product.";
                return RedirectToPage();
            }

            try
            {
                await _cartService.DecreaseAsync(productId);
                TempData["SuccessMessage"] = "Quantity updated.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decreasing quantity for product {ProductId}", productId);
                TempData["ErrorMessage"] = "An error occurred while updating quantity.";
            }

            return RedirectToPage();
        }

        /// <summary>
        /// POST: Clear entire cart
        /// </summary>
        public async Task<IActionResult> OnPostClearAsync()
        {
            try
            {
                await _cartService.ClearCartAsync();
                TempData["SuccessMessage"] = "Cart cleared successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart");
                TempData["ErrorMessage"] = "An error occurred while clearing the cart.";
            }

            return RedirectToPage();
        }
    }
}
