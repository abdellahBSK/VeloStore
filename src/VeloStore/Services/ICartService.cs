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
        /// Moves the items of a guest's cart into a user's cart. Called right after sign-in.
        /// </summary>
        /// <param name="guestSessionId">Session id the guest cart was stored under.</param>
        /// <param name="userId">Id of the user who just signed in.</param>
        Task MergeGuestCartIntoUserCartAsync(string guestSessionId, string userId);
    }
}

