using Microsoft.Extensions.Caching.Memory;

namespace Api.Services;

public sealed record FloorImage(byte[] Bytes, string ContentType);

/// <summary>Bounded, process-local cache for immutable floor-plan images.</summary>
public sealed class FloorImageCache : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 32L * 1024 * 1024 });
    // Bound concurrent cache misses without keeping a gate for every arbitrary URL id.
    private readonly SemaphoreSlim[] _missGates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly object _gate = new();
    private long _generation;

    public long Generation => Interlocked.Read(ref _generation);

    public bool TryGet(int id, out FloorImage? image) => _cache.TryGetValue(id, out image);
    public SemaphoreSlim MissGate(int id) => _missGates[(int)((uint)id % _missGates.Length)];

    public void StoreIfCurrent(int id, FloorImage image, long generation)
    {
        lock (_gate)
        {
            // A delete during the SQL read must not repopulate a deleted floor's image.
            if (_generation != generation) return;
            _cache.Set(id, image, new MemoryCacheEntryOptions()
                .SetSize(image.Bytes.LongLength)
                .SetSlidingExpiration(TimeSpan.FromMinutes(15))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1)));
        }
    }

    public void Remove(int id)
    {
        lock (_gate)
        {
            Interlocked.Increment(ref _generation);
            _cache.Remove(id);
        }
    }

    public void Dispose()
    {
        _cache.Dispose();
        foreach (var gate in _missGates) gate.Dispose();
    }
}
