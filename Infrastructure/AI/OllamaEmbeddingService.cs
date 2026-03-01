using System.Net.Http.Json;
using VeloStore.Application.Models;
using VeloStore.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace VeloStore.Infrastructure.AI;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaEmbeddingService> _logger;
    private const string Model = "nomic-embed-text";

    public OllamaEmbeddingService(IHttpClientFactory httpClientFactory, ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OllamaClient");
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { model = Model, prompt = text };
            var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);
            return result?.Embedding ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings from local Ollama.");
            throw; 
        }
    }
}
