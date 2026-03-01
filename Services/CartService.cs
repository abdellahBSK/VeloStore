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

        /// <summary>
        /// Gets the user identifier (authenticated user ID or guest session ID)
        /// </summary>
        private string GetCartIdentifier()
        {
            var httpContext = _httpContext.HttpContext;
            if (httpContext == null)
                throw new InvalidOperationException("HttpContext is not available");

            // Check if user is authenticated
            var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            // For guest users, use session ID
            var sessionId = httpContext.Session?.Id;
            if (string.IsNullOrEmpty(sessionId))
            {
                // Create session if it doesn't exist
                httpContext.Session.SetString("CartSession", "init");
                sessionId = httpContext.Session.Id;
            }

            return $"guest:{sessionId}";
        }

        /// <summary>
        /// Checks if the current user is authenticated
        /// </summary>
        private bool IsAuthenticated => 
            _httpContext.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

        private string CacheKey => $"cart:{GetCartIdentifier()}";

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
                        var identifier = GetCartIdentifier();
                        _logger.LogDebug("Cart retrieved from cache for {Identifier}", identifier);
                        return cart;
                    }
                }

                // Create new empty cart
                var identifier2 = GetCartIdentifier();
                var newCart = new RedisCartVM
                {
                    UserId = identifier2,
                    Items = new List<RedisCartItemVM>()
                };

                await SaveCartAsync(newCart);
                _logger.LogInformation("New cart created for {Identifier}", identifier2);
                return newCart;
            }
            catch (Exception ex)
            {
                var identifier = GetCartIdentifier();
                _logger.LogError(ex, "Error retrieving cart for {Identifier}", identifier);
                // Return empty cart instead of crashing
                return new RedisCartVM
                {
                    UserId = identifier,
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
                    var identifier = GetCartIdentifier();
                    _logger.LogInformation(
                        "Product {ProductId} added to cart for {Identifier}",
                        productId, identifier);
                }
                else
                {
                    item.Quantity++;
                    var identifier = GetCartIdentifier();
                    _logger.LogDebug(
                        "Product {ProductId} quantity increased to {Quantity} for {Identifier}",
                        productId, item.Quantity, identifier);
                }

                await SaveCartAsync(cart);
            }
            catch (Exception ex)
            {
                var identifier = GetCartIdentifier();
                _logger.LogError(ex,
                    "Error adding product {ProductId} to cart for {Identifier}",
                    productId, identifier);
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
                    var identifier = GetCartIdentifier();
                    _logger.LogWarning(
                        "Attempted to increase quantity of non-existent product {ProductId} for {Identifier}",
                        productId, identifier);
                    return;
                }

                item.Quantity++;
                await SaveCartAsync(cart);
                var identifier2 = GetCartIdentifier();
                _logger.LogDebug(
                    "Product {ProductId} quantity increased to {Quantity} for {Identifier}",
                    productId, item.Quantity, identifier2);
            }
            catch (Exception ex)
            {
                var identifier = GetCartIdentifier();
                _logger.LogError(ex,
                    "Error increasing quantity for product {ProductId} for {Identifier}",
                    productId, identifier);
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
                    var identifier = GetCartIdentifier();
                    _logger.LogWarning(
                        "Attempted to decrease quantity of non-existent product {ProductId} for {Identifier}",
                        productId, identifier);
                    return;
                }

                item.Quantity--;

                if (item.Quantity <= 0)
                {
                    cart.Items.Remove(item);
                    var identifier = GetCartIdentifier();
                    _logger.LogInformation(
                        "Product {ProductId} removed from cart for {Identifier}",
                        productId, identifier);
                }

                await SaveCartAsync(cart);
            }
            catch (Exception ex)
            {
                var identifier = GetCartIdentifier();
                _logger.LogError(ex,
                    "Error decreasing quantity for product {ProductId} for {Identifier}",
                    productId, identifier);
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
                var identifier = GetCartIdentifier();
                _logger.LogInformation("Cart cleared for {Identifier}", identifier);
            }
            catch (Exception ex)
            {
                var identifier = GetCartIdentifier();
                _logger.LogError(ex, "Error clearing cart for {Identifier}", identifier);
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
                var identifier = GetCartIdentifier();
                _logger.LogDebug("Cart item count: {Count} for {Identifier}", count, identifier);
                return count;
            }
            catch (Exception ex)
            {
                var identifier = GetCartIdentifier();
                _logger.LogError(ex, "Error getting cart item count for {Identifier}", identifier);
                return 0;
            }
        }

        /// <summary>
        /// Merges guest cart into user cart when user logs in
        /// </summary>
        public async Task MergeGuestCartIntoUserCartAsync(string guestSessionId)
        {
            try
            {
                if (!IsAuthenticated)
                {
                    _logger.LogWarning("Attempted to merge cart without authentication");
                    return;
                }

                var guestCartKey = $"cart:guest:{guestSessionId}";
                var guestCartJson = await _cache.GetStringAsync(guestCartKey);

                if (string.IsNullOrEmpty(guestCartJson))
                {
                    _logger.LogDebug("No guest cart found to merge");
                    return;
                }

                var guestCart = JsonSerializer.Deserialize<RedisCartVM>(guestCartJson, _jsonOptions);
                if (guestCart == null || !guestCart.Items.Any())
                {
                    _logger.LogDebug("Guest cart is empty, nothing to merge");
                    return;
                }

                // Get user cart
                var userCart = await GetCartAsync();

                // Merge items from guest cart into user cart
                foreach (var guestItem in guestCart.Items)
                {
                    var existingItem = userCart.Items.FirstOrDefault(i => i.ProductId == guestItem.ProductId);
                    if (existingItem != null)
                    {
                        // If item exists, add quantities
                        existingItem.Quantity += guestItem.Quantity;
                    }
                    else
                    {
                        // Add new item
                        userCart.Items.Add(guestItem);
                    }
                }

                // Save merged cart
                await SaveCartAsync(userCart);

                // Remove guest cart
                await _cache.RemoveAsync(guestCartKey);

                _logger.LogInformation(
                    "Merged {ItemCount} items from guest cart into user cart",
                    guestCart.Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging guest cart into user cart");
                // Don't throw - merging is not critical
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
                
                var identifier = GetCartIdentifier();
                _logger.LogDebug(
                    "Cart saved to cache for {Identifier} with {ItemCount} items",
                    identifier, cart.Items.Count);
            }
            catch (Exception ex)
            {
                var identifier = GetCartIdentifier();
                _logger.LogError(ex, "Error saving cart to cache for {Identifier}", identifier);
                throw;
            }
        }
    }
}
