using Microsoft.AspNetCore.Http;

namespace VeloStore.Tests.TestDoubles;

/// <summary>In-memory <see cref="ISession"/> with a fixed id.</summary>
public sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public TestSession(string id = "guest-session-1") => Id = id;

    public string Id { get; }
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}
