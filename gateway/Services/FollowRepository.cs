using System.Collections.Concurrent;

namespace CachaElPrecio.Gateway.Services;

public sealed class FollowRepository
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, byte>> _byUser = new();

    public IReadOnlyCollection<long> List(string user)
    {
        return _byUser.TryGetValue(user, out var products)
            ? products.Keys.Order().ToArray()
            : Array.Empty<long>();
    }

    public bool Add(string user, long productId)
    {
        var products = _byUser.GetOrAdd(user, _ => new ConcurrentDictionary<long, byte>());
        return products.TryAdd(productId, 0);
    }

    public bool Remove(string user, long productId)
    {
        return _byUser.TryGetValue(user, out var products)
            && products.TryRemove(productId, out _);
    }
}
