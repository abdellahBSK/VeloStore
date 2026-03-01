namespace VeloStore.Application.Models;

public class OllamaGenerateResponse
{
    public string Model { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public bool Done { get; set; }
}

public class OllamaEmbeddingResponse
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
