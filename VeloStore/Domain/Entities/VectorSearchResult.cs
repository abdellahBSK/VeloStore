namespace VeloStore.Domain.Entities;

public record VectorSearchResult(
    int ProductId, // Changed from Guid to int because Product.Id is int
    double SemanticSimilarity,
    double KeywordScore,
    double FinalScore
);
