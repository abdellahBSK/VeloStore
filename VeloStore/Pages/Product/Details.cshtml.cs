using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VeloStore.Data;
using VeloStore.Services;
using VeloStore.ViewModels;

namespace VeloStore.Pages.Product
{
    public class DetailsModel : PageModel
    {
        private readonly VeloStoreDbContext _context;
        private readonly CartService _cartService;

        public ProductDetailsVM Product { get; set; }

        public DetailsModel(VeloStoreDbContext context, CartService cartService)
        {
            _context = context;
            _cartService = cartService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var p = await _context.Products.FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return RedirectToPage("/Index");

            Product = new ProductDetailsVM
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                Stock = p.Stock,
                ImageUrl = p.ImageUrl
            };

            return Page();
        }

        public IActionResult OnPostAddToCart(int productId)
        {
            var product = _context.Products.First(p => p.Id == productId);

            _cartService.AddToCart(new CartItemVM
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                Quantity = 1
            });

            return RedirectToPage("/Cart/Index");
        }
    }
}
