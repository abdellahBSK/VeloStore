using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using VeloStore.ViewModels;

namespace VeloStore.Services
{
    /// <summary>
    /// Professional cart service with Redis caching, error handling, and logging
    /// Each user has their own isolated cart stored in Redis
    /// </summary>
    public class CartService : ICartService
    {
        private readonly IDistributedCache _cache;
        private readonly IHttpContextAccessor _httpContext;
        private readonly ILogger<CartService> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private const int CART_EXPIRATION_HOURS = 6;

        public CartService(
            IDistributedCache cache,
            IHttpContextAccessor httpContext,
            ILogger<CartService> logger)
        {
            _cache = cache;
            _httpContext = httpContext;
            _logger = logger;
        }

        private string UserId
        {
            get
            {
                var userId = _httpContext.HttpContext?.User
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Attempted to access cart without authentication");
                    throw new UnauthorizedAccessException("User must be authenticated to access cart");
                }

                return userId;
            }
        }

        private string CacheKey => $"cart:{UserId}";

        /// <summary>
        /// Gets the user's cart or creates a new empty cart if it doesn't exist
        /// </summary>
        public async Task<RedisCartVM> GetCartAsync()
        {
            try
            {
                var json = await _cache.GetStringAsync(CacheKey);

                if (!string.IsNullOrEmpty(json))
                {
                    var cart = JsonSerializer.Deserialize<RedisCartVM>(json, _jsonOptions);
                    if (cart != null)
                    {
                        _logger.LogDebug("Cart retrieved from cache for user {UserId}", UserId);
                        return cart;
                    }
                }

                // Create new empty cart
                var newCart = new RedisCartVM
                {
                    UserId = UserId,
                    Items = new List<RedisCartItemVM>()
                };

                await SaveCartAsync(newCart);
                _logger.LogInformation("New cart created for user {UserId}", UserId);
                return newCart;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cart for user {UserId}", UserId);
                // Return empty cart instead of crashing
                return new RedisCartVM
                {
                    UserId = UserId,
                    Items = new List<RedisCartItemVM>()
                };
            }
        }

        /// <summary>
        /// Adds a product to the cart or increments quantity if already exists
        /// </summary>
        public async Task AddToCartAsync(
            int productId,
            string productName,
            decimal price,
            string imageUrl)
        {
            try
            {
                if (price < 0)
                {
                    _logger.LogWarning("Attempted to add product with negative price {Price}", price);
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
                    _logger.LogInformation(
                        "Product {ProductId} added to cart for user {UserId}",
                        productId, UserId);
                }
                else
                {
                    item.Quantity++;
                    _logger.LogDebug(
                        "Product {ProductId} quantity increased to {Quantity} for user {UserId}",
                        productId, item.Quantity, UserId);
                }

                await SaveCartAsync(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error adding product {ProductId} to cart for user {UserId}",
                    productId, UserId);
                throw;
            }
        }

        /// <summary>
        /// Increases the quantity of a product in the cart
        /// </summary>
        public async Task IncreaseAsync(int productId)
        {
            try
            {
                var cart = await GetCartAsync();
                var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                
                if (item == null)
                {
                    _logger.LogWarning(
                        "Attempted to increase quantity of non-existent product {ProductId} for user {UserId}",
                        productId, UserId);
                    return;
                }

                item.Quantity++;
                await SaveCartAsync(cart);
                _logger.LogDebug(
                    "Product {ProductId} quantity increased to {Quantity} for user {UserId}",
                    productId, item.Quantity, UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error increasing quantity for product {ProductId} for user {UserId}",
                    productId, UserId);
                throw;
            }
        }

        /// <summary>
        /// Decreases the quantity of a product, removes if quantity reaches 0
        /// </summary>
        public async Task DecreaseAsync(int productId)
        {
            try
            {
                var cart = await GetCartAsync();
                var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                
                if (item == null)
                {
                    _logger.LogWarning(
                        "Attempted to decrease quantity of non-existent product {ProductId} for user {UserId}",
                        productId, UserId);
                    return;
                }

                item.Quantity--;

                if (item.Quantity <= 0)
                {
                    cart.Items.Remove(item);
                    _logger.LogInformation(
                        "Product {ProductId} removed from cart for user {UserId}",
                        productId, UserId);
                }

                await SaveCartAsync(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error decreasing quantity for product {ProductId} for user {UserId}",
                    productId, UserId);
                throw;
            }
        }

        /// <summary>
        /// Clears all items from the cart
        /// </summary>
        public async Task ClearCartAsync()
        {
            try
            {
                await _cache.RemoveAsync(CacheKey);
                _logger.LogInformation("Cart cleared for user {UserId}", UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart for user {UserId}", UserId);
                throw;
            }
        }

        /// <summary>
        /// Gets the total number of items in the cart
        /// </summary>
        public async Task<int> GetCartItemCountAsync()
        {
            try
            {
                var cart = await GetCartAsync();
                var count = cart.Items.Sum(i => i.Quantity);
                _logger.LogDebug("Cart item count: {Count} for user {UserId}", count, UserId);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart item count for user {UserId}", UserId);
                return 0;
            }
        }

        /// <summary>
        /// Saves the cart to Redis with sliding expiration
        /// </summary>
        private async Task SaveCartAsync(RedisCartVM cart)
        {
            try
            {
                var options = new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromHours(CART_EXPIRATION_HOURS)
                };

                var json = JsonSerializer.Serialize(cart, _jsonOptions);
                await _cache.SetStringAsync(CacheKey, json, options);
                
                _logger.LogDebug(
                    "Cart saved to cache for user {UserId} with {ItemCount} items",
                    UserId, cart.Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving cart to cache for user {UserId}", UserId);
                throw;
            }
        }
    }
}
