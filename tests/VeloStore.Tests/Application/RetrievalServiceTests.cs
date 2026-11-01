using Microsoft.Extensions.Logging.Abstractions;
using VeloStore.Application.Services;
using VeloStore.Domain.Entities;
using VeloStore.Domain.Interfaces;
using VeloStore.Services;
using VeloStore.ViewModels;

namespace VeloStore.Tests.Application;

public class RetrievalServiceTests
{
    private static RetrievalService CreateService(float[] queryVector, params VectorSearchResult[] matches) =>
        new(new StubEmbeddingService(queryVector),
            new StubVectorRepository(matches),
            new StubProductCatalog(),
            NullLogger<RetrievalService>.Instance);

    [Fact]
    public async Task Returns_nothing_when_the_question_cannot_be_embedded()
    {
        var service = CreateService(Array.Empty<float>(), new VectorSearchResult(1, 0.9, 0.5, 0.8));

        Assert.Empty(await service.RetrieveRelevantProductsAsync("bike"));
    }

    [Fact]
    public async Task Drops_weak_matches_and_orders_the_rest_by_score()
    {
        var service = CreateService(
            new[] { 0.1f },
            new VectorSearchResult(1, SemanticSimilarity: 0.55, KeywordScore: 0.1, FinalScore: 0.42),
            new VectorSearchResult(2, SemanticSimilarity: 0.20, KeywordScore: 0.0, FinalScore: 0.14), // too weak
            new VectorSearchResult(3, SemanticSimilarity: 0.80, KeywordScore: 0.4, FinalScore: 0.68));

        var results = (await service.RetrieveRelevantProductsAsync("bike")).ToList();

        Assert.Equal(new[] { 3, 1 }, results.Select(r => r.Product.Id));
    }

    [Fact]
    public async Task Keeps_at_most_four_products_and_skips_ids_missing_from_the_catalog()
    {
        var matches = Enumerable.Range(1, 8)
            .Select(id => new VectorSearchResult(id, 0.9, 0.5, 0.9 - id * 0.01))
            .Append(new VectorSearchResult(StubProductCatalog.MissingId, 0.99, 0.9, 0.99))
            .ToArray();

        var results = (await CreateService(new[] { 0.1f }, matches).RetrieveRelevantProductsAsync("bike")).ToList();

        Assert.Equal(4, results.Count);
        Assert.DoesNotContain(results, r => r.Product.Id == StubProductCatalog.MissingId);
    }

    private sealed class StubEmbeddingService(float[] vector) : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(vector);
    }

    private sealed class StubVectorRepository(VectorSearchResult[] matches) : IVectorRepository
    {
        public Task<IEnumerable<VectorSearchResult>> SearchHybridAsync(float[] queryEmbedding, string queryString, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<VectorSearchResult>>(matches);

        public Task UpsertProductEmbeddingAsync(int productId, string name, string description, decimal price, string category, float[] embedding, string version, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<List<int>> GetIndexedProductIdsAsync(string version, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<int>());
    }

    private sealed class StubProductCatalog : IProductCacheService
    {
        public const int MissingId = 404;

        public Task<ProductDetailsVM?> GetProductDetailsAsync(int productId) =>
            Task.FromResult(productId == MissingId
                ? null
                : new ProductDetailsVM { Id = productId, Name = $"Product {productId}", Description = "A bike", Price = 100m, ImageUrl = "/x.jpg" });

        public Task<List<HomeProductVM>> GetHomeProductsAsync() => throw new NotSupportedException();
        public Task<List<HomeProductVM>> GetFilteredProductsAsync(string? query, decimal? minPrice, decimal? maxPrice, string? sort) => throw new NotSupportedException();
        public Task InvalidateHomeProductsCacheAsync() => throw new NotSupportedException();
        public Task InvalidateProductCacheAsync(int productId) => throw new NotSupportedException();
    }
}
