using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VeloStore.Application.Models;
using VeloStore.Application.Options;
using VeloStore.Application.Services;
using VeloStore.Domain.Interfaces;

namespace VeloStore.Infrastructure.AI;

/// <summary>
/// Turns text into a 768-dimension vector with a local Ollama embedding model.
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IOptions<OllamaOptions> options,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient(RagOrchestrator.HttpClientName);
        _model = options.Value.EmbeddingModel;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { model = _model, prompt = text };
            var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);
            return result?.Embedding ?? Array.Empty<float>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to generate an embedding with model {Model}.", _model);
            throw;
        }
    }
}
