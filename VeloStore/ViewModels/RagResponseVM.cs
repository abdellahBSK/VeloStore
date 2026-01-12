namespace VeloStore.ViewModels
{
    /// <summary>
    /// Response model from RAG service
    /// </summary>
    public class RagResponseVM
    {
        public string Response { get; set; } = default!;
        public List<string>? ToolCalls { get; set; } // Names of tools that were executed
        public bool RequiresConfirmation { get; set; }
        public string? ConfirmationMessage { get; set; }
    }
}

