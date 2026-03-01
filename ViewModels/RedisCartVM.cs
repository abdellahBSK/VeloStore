namespace VeloStore.ViewModels
{
    public class RedisCartVM
    {
        public string UserId { get; set; } = default!;
        public List<RedisCartItemVM> Items { get; set; } = new();
    }
}
