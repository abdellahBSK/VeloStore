namespace VeloStore.ViewModels
{
    /// <summary>
    /// ViewModel for cart items stored in Redis
    /// </summary>
    public class RedisCartItemVM
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
