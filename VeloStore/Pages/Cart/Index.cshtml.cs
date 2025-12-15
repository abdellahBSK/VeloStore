using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VeloStore.Services;
using VeloStore.ViewModels;

namespace VeloStore.Pages.Cart
{
    public class IndexModel : PageModel
    {
        private readonly CartService _cartService;

        public List<CartItemVM> Items { get; set; } = new();

        public IndexModel(CartService cartService)
        {
            _cartService = cartService;
        }

        public void OnGet()
        {
            Items = _cartService.GetCart();
        }

        public IActionResult OnPostIncrease(int productId)
        {
            _cartService.Increase(productId);
            return RedirectToPage();
        }

        public IActionResult OnPostDecrease(int productId)
        {
            _cartService.Decrease(productId);
            return RedirectToPage();
        }
    }
}
