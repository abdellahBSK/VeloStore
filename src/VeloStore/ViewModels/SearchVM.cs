namespace VeloStore.ViewModels
{
    public class SearchVM
    {
        public string? Query { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Sort { get; set; }
    }
}
