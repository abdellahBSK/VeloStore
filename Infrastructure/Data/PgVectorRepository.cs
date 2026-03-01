using Npgsql;
using Pgvector;
using VeloStore.Domain.Entities;
using VeloStore.Domain.Interfaces;

namespace VeloStore.Infrastructure.Data;

public class PgVectorRepository : IVectorRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PgVectorRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task UpsertProductEmbeddingAsync(int productId, string name, string description, decimal price, string category, float[] embedding, string version, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO product_embeddings (product_id, name, description, price, category, embedding, embedding_version, is_active)
            VALUES ($1, $2, $3, $4, $5, $6, $7, true)
            ON CONFLICT (product_id) DO UPDATE 
            SET name = EXCLUDED.name,
                description = EXCLUDED.description,
                price = EXCLUDED.price,
                category = EXCLUDED.category,
                embedding = EXCLUDED.embedding,
                embedding_version = EXCLUDED.embedding_version,
                updated_at = CURRENT_TIMESTAMP;
        ";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        
        cmd.Parameters.Add(new NpgsqlParameter { Value = productId });
        cmd.Parameters.Add(new NpgsqlParameter { Value = name ?? "" });
        cmd.Parameters.Add(new NpgsqlParameter { Value = description ?? "" });
        cmd.Parameters.Add(new NpgsqlParameter { Value = price });
        cmd.Parameters.Add(new NpgsqlParameter { Value = category ?? "" });
        cmd.Parameters.Add(new NpgsqlParameter { Value = new Vector(embedding) });
        cmd.Parameters.Add(new NpgsqlParameter { Value = version });

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchHybridAsync(float[] queryEmbedding, string queryString, int limit, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                product_id,
                (1.0 - (embedding <=> $1)) AS semantic_score,
                ts_rank(search_vector, websearch_to_tsquery('english', $2)) AS keyword_score,
                ((0.7 * (1 - (embedding <=> $1))) + (0.3 * ts_rank(search_vector, websearch_to_tsquery('english', $2)))) AS final_score
            FROM product_embeddings
            WHERE is_active = true
            ORDER BY final_score DESC
            LIMIT $3;
        ";

        var results = new List<VectorSearchResult>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        
        cmd.Parameters.Add(new NpgsqlParameter { Value = new Vector(queryEmbedding) });
        cmd.Parameters.Add(new NpgsqlParameter { Value = queryString });
        cmd.Parameters.Add(new NpgsqlParameter { Value = limit });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new VectorSearchResult(
                ProductId: reader.GetInt32(0),
                SemanticSimilarity: reader.GetDouble(1),
                KeywordScore: reader.GetDouble(2),
                FinalScore: reader.GetDouble(3)
            ));
        }

        return results;
    }

    public async Task<List<int>> GetIndexedProductIdsAsync(string version, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT product_id FROM product_embeddings WHERE embedding_version = $1";
        var results = new List<int>();
        
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter { Value = version });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(reader.GetInt32(0));
        }

        return results;
    }
}
