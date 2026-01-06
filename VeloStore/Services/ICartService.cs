using VeloStore.ViewModels;

namespace VeloStore.Services
{
    /// <summary>
    /// Service interface for managing user shopping carts
    /// </summary>
    public interface ICartService
    {
        /// <summary>
        /// Gets the current user's cart or creates a new one if it doesn't exist
        /// </summary>
        Task<RedisCartVM> GetCartAsync();

        /// <summary>
        /// Adds a product to the cart or increments quantity if already exists
        /// </summary>
        Task AddToCartAsync(int productId, string productName, decimal price, string imageUrl);

        /// <summary>
        /// Increases the quantity of a product in the cart
        /// </summary>
        Task IncreaseAsync(int productId);

        /// <summary>
        /// Decreases the quantity of a product in the cart, removes if quantity reaches 0
        /// </summary>
        Task DecreaseAsync(int productId);

        /// <summary>
        /// Clears all items from the cart
        /// </summary>
        Task ClearCartAsync();

        /// <summary>
        /// Gets the total number of items in the cart
        /// </summary>
        Task<int> GetCartItemCountAsync();

        /// <summary>
        /// Merges guest cart into user cart when user logs in
        /// </summary>
        Task MergeGuestCartIntoUserCartAsync(string guestSessionId);
    }
}

