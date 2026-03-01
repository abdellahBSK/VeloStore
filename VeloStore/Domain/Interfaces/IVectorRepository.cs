using VeloStore.Domain.Entities;

namespace VeloStore.Domain.Interfaces;

public interface IVectorRepository
{
    Task UpsertProductEmbeddingAsync(int productId, string name, string description, decimal price, string category, float[] embedding, string version, CancellationToken cancellationToken = default);
    Task<IEnumerable<VectorSearchResult>> SearchHybridAsync(float[] queryEmbedding, string queryString, int limit, CancellationToken cancellationToken = default);
    Task<List<int>> GetIndexedProductIdsAsync(string version, CancellationToken cancellationToken = default);
}
