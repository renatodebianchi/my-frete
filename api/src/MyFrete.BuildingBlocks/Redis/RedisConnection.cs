using StackExchange.Redis;

namespace MyFrete.BuildingBlocks.Redis;

/// <summary>Shared multiplexer + small helpers for TTL keys and best-effort distributed locks.</summary>
public interface IRedisConnection
{
    IConnectionMultiplexer Multiplexer { get; }

    IDatabase Db { get; }

    Task<bool> SetIfAbsentAsync(string key, string value, TimeSpan ttl);

    Task<IAsyncDisposable?> AcquireLockAsync(string resource, TimeSpan ttl, CancellationToken ct = default);
}

public sealed class RedisConnection(IConnectionMultiplexer multiplexer) : IRedisConnection
{
    public IConnectionMultiplexer Multiplexer => multiplexer;

    public IDatabase Db => multiplexer.GetDatabase();

    public Task<bool> SetIfAbsentAsync(string key, string value, TimeSpan ttl) =>
        Db.StringSetAsync(key, value, ttl, When.NotExists);

    public async Task<IAsyncDisposable?> AcquireLockAsync(string resource, TimeSpan ttl, CancellationToken ct = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var key = $"lock:{resource}";

        if (!await Db.StringSetAsync(key, token, ttl, When.NotExists))
        {
            return null;
        }

        return new Lock(Db, key, token);
    }

    private sealed class Lock(IDatabase db, string key, string token) : IAsyncDisposable
    {
        private const string ReleaseScript =
            "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

        public async ValueTask DisposeAsync() =>
            await db.ScriptEvaluateAsync(ReleaseScript, [key], [token]);
    }
}
