using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace UrbanShade.Infraestructure.DataAcess;

// A dedicated cache avoids imposing Size requirements on unrelated IMemoryCache users.
public sealed class InstitutionalDashboardCache : IDisposable
{
    public const long Capacity = 16 * 1024 * 1024;
    public const long MaximumEntrySize = 1024 * 1024;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = Capacity });
    private static string Key(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public bool TryGetValue<T>(string key, out T? value) => _cache.TryGetValue(Key(key), out value);
    public bool Set<T>(string key, T value, DateTimeOffset expires)
    {
        // Account conservatively for serialized data, object overhead and the hashed key.
        var size = JsonSerializer.SerializeToUtf8Bytes(value).LongLength * 4 + 1024;
        if (size > MaximumEntrySize) return false;
        _cache.Set(Key(key), value, new MemoryCacheEntryOptions { AbsoluteExpiration = expires, Size = size });
        return true;
    }
    public void Dispose() => _cache.Dispose();
}
