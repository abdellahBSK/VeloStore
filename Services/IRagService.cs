using VeloStore.ViewModels;

namespace VeloStore.Services
{
    /// <summary>
    /// RAG service interface for AI-powered shopping assistant
    /// Orchestrates calls to existing services without replacing them
    /// </summary>
    public interface IRagService
    {
        /// <summary>
        /// Processes user message and returns AI response with tool execution
        /// </summary>
        Task<RagResponseVM> ProcessMessageAsync(RagRequestVM request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets conversation history (if needed for context)
        /// </summary>
        Task<List<RagMessageVM>> GetConversationHistoryAsync(string conversationId, CancellationToken cancellationToken = default);
    }
}

