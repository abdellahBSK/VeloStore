using System.Diagnostics;
using VeloStore.Application.Models;
using VeloStore.Domain.Interfaces;
using VeloStore.Services;
using Microsoft.Extensions.Logging;

namespace VeloStore.Application.Services;

public class RetrievalService : IRetrievalService
{
    private readonly IEmbeddingService _embedding;
    private readonly IVectorRepository _vectorRepo;
    private readonly IProductCacheService _cache;
    private readonly ILogger<RetrievalService> _logger;
    private const double SIMILARITY_THRESHOLD = 0.40;

    public RetrievalService(
        IEmbeddingService embedding, 
        IVectorRepository vectorRepo, 
        IProductCacheService cache,
        ILogger<RetrievalService> logger)
    {
        _embedding = embedding;
        _vectorRepo = vectorRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<ProductContext>> RetrieveRelevantProductsAsync(string query, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        var queryVector = await _embedding.GenerateEmbeddingAsync(query, cancellationToken);
        if (queryVector.Length == 0) return Enumerable.Empty<ProductContext>();

        var allMatches = await _vectorRepo.SearchHybridAsync(queryVector, query, limit: 10, cancellationToken);

        var validMatches = new List<ProductContext>();

        foreach (var match in allMatches)
        {
            _logger.LogInformation("Match {Id} | Semantic: {Semantic:F3} | Keyword: {Keyword:F3} | Final: {Final:F3}", 
                match.ProductId, match.SemanticSimilarity, match.KeywordScore, match.FinalScore);

            if (match.SemanticSimilarity >= SIMILARITY_THRESHOLD || match.FinalScore > 0.3)
            {
                var product = await _cache.GetProductDetailsAsync(match.ProductId);
                if (product != null)
                {
                    validMatches.Add(new ProductContext(product, match.FinalScore));
                }
            }
        }
        
        _logger.LogInformation("Retrieval completed in {ElapsedMs}ms. Found {Count} results above threshold.", 
            sw.ElapsedMilliseconds, validMatches.Count);

        return validMatches.OrderByDescending(x => x.Score).Take(4);
    }
}
