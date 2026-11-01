using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VeloStore.Application.AI.Prompts;
using VeloStore.Application.Models;
using VeloStore.Application.Options;
using VeloStore.Application.Security;
using VeloStore.Domain.Interfaces;

namespace VeloStore.Application.Services;

/// <summary>
/// Answers a shopper's question with retrieval-augmented generation:
/// sanitize the question, retrieve matching products, then ask the local LLM
/// to answer using only those products.
/// </summary>
public class RagOrchestrator : IRagOrchestrator
{
    /// <summary>Name of the <see cref="HttpClient"/> registered for Ollama.</summary>
    public const string HttpClientName = "OllamaClient";

    internal const string NoMatchAnswer =
        "I don't know or I'm not sure. We couldn't find any products closely matching your request in our catalog.";

    internal const string UnavailableAnswer =
        "The assistant is unavailable right now. Please try again in a moment.";

    private const int MaxDescriptionLength = 200;

    private readonly IRetrievalService _retrievalService;
    private readonly HttpClient _llmClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<RagOrchestrator> _logger;

    public RagOrchestrator(
        IRetrievalService retrievalService,
        IHttpClientFactory clientFactory,
        IOptions<OllamaOptions> options,
        ILogger<RagOrchestrator> logger)
    {
        _retrievalService = retrievalService;
        _llmClient = clientFactory.CreateClient(HttpClientName);
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> AnswerQueryAsync(string userQuery, CancellationToken cancellationToken = default)
    {
        var safeQuery = PromptSanitizer.Sanitize(userQuery);
        if (safeQuery.Length == 0)
        {
            return NoMatchAnswer;
        }

        List<ProductContext> contexts;
        try
        {
            contexts = (await _retrievalService.RetrieveRelevantProductsAsync(safeQuery, cancellationToken)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Product retrieval failed.");
            return UnavailableAnswer;
        }

        if (contexts.Count == 0)
        {
            return NoMatchAnswer;
        }

        var prompt = SystemPrompts.GenerateStrictRagPrompt(BuildContext(contexts), safeQuery);
        var request = new
        {
            model = _options.ChatModel,
            prompt,
            stream = false,
            options = new
            {
                num_predict = _options.MaxResponseTokens,
                temperature = _options.Temperature,
                num_ctx = _options.ContextWindow
            }
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await _llmClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);

            _logger.LogInformation(
                "LLM answered in {ElapsedMs} ms using {ProductCount} products.",
                stopwatch.ElapsedMilliseconds, contexts.Count);

            return string.IsNullOrWhiteSpace(result?.Response)
                ? UnavailableAnswer
                : result.Response.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "LLM call failed after {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
            return UnavailableAnswer;
        }
    }

    /// <summary>
    /// Formats the retrieved products as the prompt's CONTEXT block.
    /// Product text is sanitized too, because catalog data also ends up inside the prompt.
    /// </summary>
    internal static string BuildContext(IEnumerable<ProductContext> contexts)
    {
        var builder = new StringBuilder();
        foreach (var context in contexts)
        {
            var product = context.Product;
            var description = product.Description ?? string.Empty;
            if (description.Length > MaxDescriptionLength)
            {
                description = description[..MaxDescriptionLength] + "...";
            }

            var name = PromptSanitizer.Sanitize(product.Name ?? string.Empty);
            description = PromptSanitizer.Sanitize(description);

            builder.AppendLine($"- {name}: {FormatPrice(product.Price)}. {description}");
        }

        return builder.ToString();
    }

    /// <summary>Prices are shown in Moroccan dirhams across the store, e.g. "1,299.99 MAD".</summary>
    internal static string FormatPrice(decimal price) =>
        price.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + " MAD";
}
