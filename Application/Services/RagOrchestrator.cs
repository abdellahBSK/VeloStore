using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using VeloStore.Application.AI.Prompts;
using VeloStore.Application.Models;
using VeloStore.Application.Security;
using VeloStore.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace VeloStore.Application.Services;

public class RagOrchestrator : IRagOrchestrator
{
    private readonly IRetrievalService _retrievalService;
    private readonly HttpClient _llmClient;
    private readonly ILogger<RagOrchestrator> _logger;

    public RagOrchestrator(IRetrievalService retrievalService, IHttpClientFactory clientFactory, ILogger<RagOrchestrator> logger)
    {
        _retrievalService = retrievalService;
        _llmClient = clientFactory.CreateClient("OllamaClient");
        _logger = logger;
    }

    public async Task<string> AnswerQueryAsync(string userQuery, CancellationToken cancellationToken = default)
    {
        string safeQuery = PromptSanitizer.Sanitize(userQuery);
        var sw = Stopwatch.StartNew();
        
        var contexts = await _retrievalService.RetrieveRelevantProductsAsync(safeQuery, cancellationToken);
        
        if (!contexts.Any())
        {
            return "I don't know or I'm not sure. We couldn't find any products closely matching your request in our catalog.";
        }

        var contextBuilder = new StringBuilder();
        foreach (var c in contexts)
        {
            // Safely limit description length to avoid large context overhead on 8GB RAM machines
            var shortDesc = c.Product.Description != null && c.Product.Description.Length > 200 
                ? c.Product.Description.Substring(0, 200) 
                : (c.Product.Description ?? "");
                
            contextBuilder.AppendLine($"- {c.Product.Name}: {c.Product.Price:C}. {shortDesc}...");
        }

        var prompt = SystemPrompts.GenerateStrictRagPrompt(contextBuilder.ToString(), safeQuery);

        var request = new { 
            model = "phi3:mini", 
            prompt = prompt,
            stream = false,
            options = new {
                num_predict = 150, 
                temperature = 0.05, 
                num_ctx = 2048 
            }
        };

        try
        {
            var response = await _llmClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
            
            _logger.LogInformation("LLM Generation took {ElapsedMs}ms. Context chars: {ContextSize}. Returned chars: {ResSize}.", 
                sw.ElapsedMilliseconds, contextBuilder.Length, result?.Response?.Length ?? 0);

            return result?.Response ?? "Sorry, an error occurred while processing.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call LLM.");
            return "I am currently undergoing maintenance and cannot process questions. Please try again soon.";
        }
    }
}


/*
public async Task<string> AnswerQueryAsync(string userQuery, CancellationToken cancellationToken = default)
{
    // Sanitize the raw user input
    var sanitizeResult = PromptSanitizer.Sanitize(userQuery);
    
    if (sanitizeResult.WasModified)
        _logger.LogWarning("Prompt injection attempt detected. Stripped tokens: {Tokens}",
            string.Join(", ", sanitizeResult.DetectedTokens));

    // Materialise immediately — never enumerate IEnumerable<T> twice
    var contexts = (await _retrievalService.RetrieveRelevantProductsAsync(
        sanitizeResult.Value, cancellationToken)).ToList();

    if (contexts.Count == 0)
        return "I don't know or I'm not sure. We couldn't find any products " +
               "closely matching your request in our catalog.";

    var contextBuilder = new StringBuilder();
    foreach (var c in contexts)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var desc = c.Product.Description ?? string.Empty;

        // Safe Unicode-aware slice — avoids splitting surrogate pairs
        if (desc.Length > 200)
            desc = desc.EnumerateRunes().Take(200)
                       .Aggregate(new StringBuilder(), (sb, r) => { sb.Append(r); return sb; })
                       .ToString();

        // Sanitize database content before it enters the prompt
        var safeName  = PromptSanitizer.Sanitize(c.Product.Name  ?? string.Empty).Value;
        var safeDesc  = PromptSanitizer.Sanitize(desc).Value;

        contextBuilder.AppendLine($"- {safeName}: {c.Product.Price:C}. {safeDesc}...");
    }

    var prompt = SystemPrompts.GenerateStrictRagPrompt(
        contextBuilder.ToString(), sanitizeResult.Value);

    var request = new
    {
        model   = "phi3:mini",
        prompt  = prompt,
        stream  = false,
        options = new { num_predict = 150, temperature = 0.05, num_ctx = 2048 }
    };

    // Separate stopwatches for retrieval vs generation
    var llmSw = Stopwatch.StartNew();

    try
    {
        var response = await _llmClient.PostAsJsonAsync(
            "/api/generate", request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
            cancellationToken: cancellationToken);

        llmSw.Stop();

        // Distinguish silent-empty from actual error
        if (string.IsNullOrWhiteSpace(result?.Response))
        {
            _logger.LogWarning("LLM returned an empty response after {ElapsedMs}ms.", 
                llmSw.ElapsedMilliseconds);
            return "Sorry, I was unable to generate a response. Please try again.";
        }

        _logger.LogInformation(
            "LLM generation took {ElapsedMs}ms. Context chars: {ContextSize}. Response chars: {ResSize}.",
            llmSw.ElapsedMilliseconds, contextBuilder.Length, result.Response.Length);

        return result.Response;
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("LLM request was cancelled after {ElapsedMs}ms.", 
            llmSw.ElapsedMilliseconds);
        throw; // Let the caller handle cancellation — don't swallow it
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to call LLM after {ElapsedMs}ms.", 
            llmSw.ElapsedMilliseconds);
        return "I am currently undergoing maintenance. Please try again soon.";
    }
}
*/