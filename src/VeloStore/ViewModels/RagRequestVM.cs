namespace VeloStore.ViewModels
{
    /// <summary>
    /// Request model for RAG service interactions
    /// </summary>
    public class RagRequestVM
    {
        public string Message { get; set; } = default!;
        public string? ConversationId { get; set; } // For maintaining conversation context
    }
}

