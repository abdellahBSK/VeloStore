using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VeloStore.Application.Options;
using VeloStore.ViewModels;

namespace VeloStore.Services
{
    /// <summary>
    /// Shopping cart stored in Redis (through <see cref="IDistributedCache"/>).
    /// Signed-in users get the key <c>cart:user:{userId}</c>; guests get <c>cart:guest:{sessionId}</c>.
    /// Carts use a sliding expiration, so an active cart never expires.
    /// </summary>
    public class CartService : ICartService
    {
        /// <summary>
        /// Session key written for guests. ASP.NET Core only sends the session cookie once
        /// something is stored in the session; without this marker a guest would get a new
        /// session id (and an empty cart) on every request.
        /// </summary>
        internal const string GuestSessionMarkerKey = "VeloStore.CartSession";

        private readonly IDistributedCache _cache;
        private readonly IHttpContextAccessor _httpContext;
        private readonly ILogger<CartService> _logger;
        private readonly TimeSpan _cartExpiration;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public CartService(
            IDistributedCache cache,
            IHttpContextAccessor httpContext,
            IOptions<CacheOptions> cacheOptions,
            ILogger<CartService> logger)
        {
            _cache = cache;
            _httpContext = httpContext;
            _logger = logger;
            _cartExpiration = TimeSpan.FromHours(cacheOptions.Value.CartExpirationHours);
        }

        internal static string UserCartKey(string userId) => $"cart:user:{userId}";
        internal static string GuestCartKey(string sessionId) => $"cart:guest:{sessionId}";

        /// <summary>
        /// Returns the cart owner for the current request: the signed-in user's id,
        /// or the guest's session id.
        /// </summary>
        private string GetCartIdentifier()
        {
            var httpContext = _httpContext.HttpContext
                ?? throw new InvalidOperationException("HttpContext is not available");

            var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            var session = httpContext.Session;
            if (!session.Keys.Contains(GuestSessionMarkerKey))
            {
                // Writing to the session makes ASP.NET Core issue the session cookie,
                // so the guest keeps the same session id (and cart) on the next request.
                session.SetString(GuestSessionMarkerKey, "1");
            }

            return $"guest:{session.Id}";
        }

        private string CacheKey => $"cart:{GetCartIdentifier()}";

        /// <inheritdoc />
        public async Task<RedisCartVM> GetCartAsync()
        {
            var identifier = GetCartIdentifier();
            try
            {
                var cart = await ReadCartAsync($"cart:{identifier}");
                if (cart != null)
                {
                    return cart;
                }

                // An empty cart is not saved; it is created on the first add.
                return new RedisCartVM { UserId = identifier };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cart for {Identifier}", identifier);
                return new RedisCartVM { UserId = identifier };
            }
        }

        /// <inheritdoc />
        public async Task AddToCartAsync(int productId, string productName, decimal price, string imageUrl)
        {
            if (price < 0)
            {
                throw new ArgumentException("Product price cannot be negative", nameof(price));
            }

            var cart = await GetCartAsync();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item == null)
            {
                cart.Items.Add(new RedisCartItemVM
                {
                    ProductId = productId,
                    ProductName = productName,
                    Price = price,
                    ImageUrl = imageUrl,
                    Quantity = 1
                });
                _logger.LogInformation("Product {ProductId} added to cart {Identifier}", productId, cart.UserId);
            }
            else
            {
                item.Quantity++;
            }

            await SaveCartAsync(CacheKey, cart);
        }

        /// <inheritdoc />
        public async Task IncreaseAsync(int productId)
        {
            var cart = await GetCartAsync();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item == null)
            {
                _logger.LogWarning("Product {ProductId} is not in cart {Identifier}", productId, cart.UserId);
                return;
            }

            item.Quantity++;
            await SaveCartAsync(CacheKey, cart);
        }

        /// <inheritdoc />
        public async Task DecreaseAsync(int productId)
        {
            var cart = await GetCartAsync();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item == null)
            {
                _logger.LogWarning("Product {ProductId} is not in cart {Identifier}", productId, cart.UserId);
                return;
            }

            item.Quantity--;
            if (item.Quantity <= 0)
            {
                cart.Items.Remove(item);
            }

            await SaveCartAsync(CacheKey, cart);
        }

        /// <inheritdoc />
        public async Task ClearCartAsync()
        {
            await _cache.RemoveAsync(CacheKey);
            _logger.LogInformation("Cart cleared for {Identifier}", GetCartIdentifier());
        }

        /// <inheritdoc />
        public async Task<int> GetCartItemCountAsync()
        {
            var cart = await GetCartAsync();
            return cart.Items.Sum(i => i.Quantity);
        }

        /// <inheritdoc />
        public async Task MergeGuestCartIntoUserCartAsync(string guestSessionId, string userId)
        {
            if (string.IsNullOrEmpty(guestSessionId) || string.IsNullOrEmpty(userId))
            {
                return;
            }

            try
            {
                var guestKey = GuestCartKey(guestSessionId);
                var guestCart = await ReadCartAsync(guestKey);
                if (guestCart == null || guestCart.Items.Count == 0)
                {
                    return;
                }

                // Uses the user id explicitly: right after sign-in, the current request
                // is still anonymous, so the claims principal cannot be used here.
                var userKey = UserCartKey(userId);
                var userCart = await ReadCartAsync(userKey) ?? new RedisCartVM { UserId = $"user:{userId}" };

                foreach (var guestItem in guestCart.Items)
                {
                    var existing = userCart.Items.FirstOrDefault(i => i.ProductId == guestItem.ProductId);
                    if (existing != null)
                    {
                        existing.Quantity += guestItem.Quantity;
                    }
                    else
                    {
                        userCart.Items.Add(guestItem);
                    }
                }

                await SaveCartAsync(userKey, userCart);
                await _cache.RemoveAsync(guestKey);

                _logger.LogInformation(
                    "Merged {ItemCount} guest cart lines into the cart of user {UserId}",
                    guestCart.Items.Count, userId);
            }
            catch (Exception ex)
            {
                // Merging is a convenience; it must never block a successful sign-in.
                _logger.LogError(ex, "Error merging guest cart into user cart");
            }
        }

        private async Task<RedisCartVM?> ReadCartAsync(string key)
        {
            var json = await _cache.GetStringAsync(key);
            return string.IsNullOrEmpty(json)
                ? null
                : JsonSerializer.Deserialize<RedisCartVM>(json, _jsonOptions);
        }

        private async Task SaveCartAsync(string key, RedisCartVM cart)
        {
            var options = new DistributedCacheEntryOptions { SlidingExpiration = _cartExpiration };
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(cart, _jsonOptions), options);
        }
    }
}
