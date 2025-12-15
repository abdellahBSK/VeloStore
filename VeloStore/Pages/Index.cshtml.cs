using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VeloStore.Data;
using VeloStore.Services;
using VeloStore.ViewModels;

namespace VeloStore.Pages
{
    public class IndexModel : PageModel
    {
        private readonly VeloStoreDbContext _context;
        private readonly CartService _cartService;

        public IndexModel(VeloStoreDbContext context, CartService cartService)
        {
            _context = context;
            _cartService = cartService;
        }

        public List<HomeProductVM> Products { get; set; } = new();

        public SearchVM Search { get; set; } = new();

        public async Task OnGetAsync(string? query, decimal? minPrice, decimal? maxPrice, string? sort)
        {
            Search = new SearchVM
            {
                Query = query,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Sort = sort
            };

            var productsQuery = _context.Products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
                productsQuery = productsQuery.Where(p => p.Name.Contains(query));

            if (minPrice.HasValue)
                productsQuery = productsQuery.Where(p => p.Price >= minPrice);

            if (maxPrice.HasValue)
                productsQuery = productsQuery.Where(p => p.Price <= maxPrice);

            productsQuery = sort switch
            {
                "price_asc" => productsQuery.OrderBy(p => p.Price),
                "price_desc" => productsQuery.OrderByDescending(p => p.Price),
                _ => productsQuery
            };

            Products = await productsQuery.Select(p => new HomeProductVM
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                ImageUrl = p.ImageUrl
            }).ToListAsync();
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

            return RedirectToPage();
        }
    }
}
