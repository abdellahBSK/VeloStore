namespace VeloStore.ViewModels
{
    /// <summary>
    /// ViewModel for RAG chat messages
    /// </summary>
    public class RagMessageVM
    {
        public string Role { get; set; } = default!; // "user" or "assistant"
        public string Content { get; set; } = default!;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

