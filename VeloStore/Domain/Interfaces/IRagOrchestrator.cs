namespace VeloStore.Domain.Interfaces;

public interface IRagOrchestrator
{
    Task<string> AnswerQueryAsync(string userQuery, CancellationToken cancellationToken = default);
}
