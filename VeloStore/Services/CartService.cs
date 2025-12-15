using System.Text.Json;
using VeloStore.ViewModels;

namespace VeloStore.Services
{
    public class CartService
    {
        private readonly ISession _session;
        private const string CartKey = "CART";

        public CartService(IHttpContextAccessor accessor)
        {
            _session = accessor.HttpContext.Session;
        }

        public List<CartItemVM> GetCart()
        {
            var data = _session.GetString(CartKey);
            return data == null
                ? new List<CartItemVM>()
                : JsonSerializer.Deserialize<List<CartItemVM>>(data);
        }

        public void SaveCart(List<CartItemVM> cart)
        {
            _session.SetString(CartKey, JsonSerializer.Serialize(cart));
        }

        public void AddToCart(CartItemVM item)
        {
            var cart = GetCart();
            var existing = cart.FirstOrDefault(p => p.ProductId == item.ProductId);

            if (existing != null)
                existing.Quantity++;
            else
                cart.Add(item);

            SaveCart(cart);
        }

        public void Remove(int productId)
        {
            var cart = GetCart();
            cart.RemoveAll(p => p.ProductId == productId);
            SaveCart(cart);
        }
        public void Increase(int productId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(p => p.ProductId == productId);
            if (item != null) item.Quantity++;
            SaveCart(cart);
        }

        public void Decrease(int productId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(p => p.ProductId == productId);
            if (item != null)
            {
                item.Quantity--;
                if (item.Quantity <= 0)
                    cart.Remove(item);
            }
            SaveCart(cart);
        }
    }
}
