using VeloStore.Application.Models;

namespace VeloStore.Domain.Interfaces;

public interface IRetrievalService
{
    Task<IEnumerable<ProductContext>> RetrieveRelevantProductsAsync(string userQuery, CancellationToken cancellationToken = default);
}
