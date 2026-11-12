using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrbanShade.Domain.Routing;
using UrbanShade.Exceptions;

namespace UrbanShade.Infraestructure.Routing;

public sealed class OpenRouteServiceOptions
{
    [ConfigurationKeyName("Api_Key")]
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class RoutingGeometryCache : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 64 * 1024 * 1024 });

    public IReadOnlyList<RouteCandidate>? Get(string key) => _cache.TryGetValue<IReadOnlyList<RouteCandidate>>(key,out var routes) ? routes : null;

    public void Set(string key,IReadOnlyList<RouteCandidate> routes,TimeSpan duration)
    {
        var size = routes.Sum(route => (long)route.Geometry.NumPoints * 64 + route.Instructions.Sum(step => (long)step.Text.Length * 2 + 128) + 1024);
        _cache.Set(key,routes,new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = duration, Size = size });
    }

    public void Dispose() => _cache.Dispose();
}

public sealed class OpenRouteServiceProvider : IRoutingProvider
{
    private readonly HttpClient _client;
    private readonly RoutingSettings _settings;
    private readonly OpenRouteServiceOptions _credentials;
    private readonly RoutingGeometryCache _cache;
    private readonly OpenRouteServiceResponseParser _parser;
    private readonly ILogger<OpenRouteServiceProvider> _logger;

    public OpenRouteServiceProvider(HttpClient client, RoutingSettings settings, IOptions<OpenRouteServiceOptions> credentials,
        RoutingGeometryCache cache, OpenRouteServiceResponseParser parser, ILogger<OpenRouteServiceProvider> logger)
    {
        _client = client;
        _settings = settings;
        _credentials = credentials.Value;
        _cache = cache;
        _parser = parser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RouteCandidate>> GetRoutes(RoutePoint origin, RoutePoint destination, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_credentials.ApiKey))
            throw new RoutingException(UrbanShade.Exceptions.ErrorKind.Unavailable, "Routing integration is unavailable.");
        var identity = JsonSerializer.Serialize(new { origin, destination, _settings.Profile, _settings.MaxAlternativeRoutes, _settings.GreenRoutesEnabled, _settings.MaximumWalkingDistanceMeters, _settings.MaximumGeometryPoints });
        var key = "routing:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        var cached = _cache.Get(key);
        if (cached is not null) return cached;
        var timer = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
        try
        {
            var routes = (await Request(origin, destination, "conventional", timeout.Token)).ToList();
            if (routes.Count == 0) throw new RoutingException(UrbanShade.Exceptions.ErrorKind.Unprocessable, "No walking route was found between these points.");
            if (_settings.MaxAlternativeRoutes > 1)
            {
                await AddOptional("alternatives");
                if (_settings.GreenRoutesEnabled) await AddOptional("green");
            }
            IReadOnlyList<RouteCandidate> result = routes.Select((item, index) => item with { Id = $"route-{index + 1}" }).ToList();
            if (_settings.CacheDurationMinutes > 0) _cache.Set(key, result, TimeSpan.FromMinutes(_settings.CacheDurationMinutes));
            _logger.LogInformation("Routing provider returned {RouteCount} routes in {DurationMs} ms", result.Count, timer.ElapsedMilliseconds);
            return result;

            async Task AddOptional(string mode)
            {
                try { routes.AddRange(await Request(origin, destination, mode, timeout.Token)); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { _logger.LogWarning("Optional routing request exceeded its time budget"); }
                catch (RoutingException exception) { _logger.LogWarning("Optional routing request failed with status {StatusCode}", (int)exception.GetErrorKind()); }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new RoutingException(UrbanShade.Exceptions.ErrorKind.Unavailable, "Routing provider timed out. Try again shortly."); }
    }

    private async Task<IReadOnlyList<RouteCandidate>> Request(RoutePoint origin, RoutePoint destination, string mode, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>
        {
            ["coordinates"] = new[] { new[] { origin.Longitude, origin.Latitude }, new[] { destination.Longitude, destination.Latitude } },
            ["instructions"] = true, ["language"] = "pt", ["preference"] = "recommended",
            ["radiuses"] = new[] { _settings.MaximumSnapDistanceMeters, _settings.MaximumSnapDistanceMeters }
        };
        if (mode == "alternatives") body["alternative_routes"] = new { target_count = _settings.MaxAlternativeRoutes, share_factor = 0.6, weight_factor = 1.4 };
        if (mode == "green") body["options"] = new { profile_params = new { weightings = new { green = 1 } } };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl.TrimEnd('/')}/directions/{_settings.Profile}/geojson") { Content = JsonContent.Create(body) };
                request.Headers.TryAddWithoutValidation("Authorization", _credentials.ApiKey);
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests) && attempt < _settings.MaxRetryAttempts)
                {
                    var delay = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(attempt + 1);
                    if (delay > TimeSpan.FromSeconds(_settings.TimeoutSeconds))
                        throw new RoutingException(UrbanShade.Exceptions.ErrorKind.Unavailable, "Routing provider request limit reached. Try again later.");
                    await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, cancellationToken);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                    throw new RoutingException(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound ? ErrorKind.Unprocessable : ErrorKind.Unavailable,
                        response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound ? "These points could not be served by the walking provider." : "Routing provider is temporarily unavailable.");
                if (response.Content.Headers.ContentLength > _settings.MaximumResponseBytes) throw new FormatException("Routing response is too large.");
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                int count;
                while ((count = await stream.ReadAsync(chunk, cancellationToken)) > 0)
                {
                    if (buffer.Length + count > _settings.MaximumResponseBytes) throw new FormatException("Routing response is too large.");
                    buffer.Write(chunk, 0, count);
                }
                using var json = JsonDocument.Parse(buffer.ToArray());
                return _parser.Parse(json.RootElement, mode == "conventional");
            }
            catch (HttpRequestException) when (attempt < _settings.MaxRetryAttempts) { await Task.Delay(TimeSpan.FromSeconds(attempt + 1), cancellationToken); }
            catch (HttpRequestException) { throw new RoutingException(UrbanShade.Exceptions.ErrorKind.Unavailable, "Routing provider is temporarily unreachable."); }
            catch (Exception exception) when (exception is JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
            { throw new RoutingException(UrbanShade.Exceptions.ErrorKind.InvalidUpstreamResponse, "Routing provider returned an invalid response."); }
        }
    }
}
