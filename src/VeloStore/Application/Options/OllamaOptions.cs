namespace VeloStore.Application.Options;

/// <summary>
/// Settings for the local Ollama server used by the AI shopping assistant.
/// Bound from the <c>Ollama</c> configuration section.
/// </summary>
public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    /// <summary>Base URL of the Ollama HTTP API.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Model used to generate answers.</summary>
    public string ChatModel { get; set; } = "phi3:mini";

    /// <summary>Model used to embed products and questions (768 dimensions).</summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>Maximum number of tokens the chat model may generate.</summary>
    public int MaxResponseTokens { get; set; } = 150;

    /// <summary>Sampling temperature. Kept low so answers stick to the retrieved context.</summary>
    public double Temperature { get; set; } = 0.05;

    /// <summary>Context window size passed to the chat model.</summary>
    public int ContextWindow { get; set; } = 2048;
}
