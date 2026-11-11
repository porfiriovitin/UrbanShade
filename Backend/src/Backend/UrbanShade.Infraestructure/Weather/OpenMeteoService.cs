using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrbanShade.Domain.Weather;

namespace UrbanShade.Infraestructure.Weather;

public sealed class OpenMeteoService : IOpenMeteoService
{
    private readonly HttpClient _client;
    private readonly OpenMeteoSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<OpenMeteoService> _logger;
    public OpenMeteoService(HttpClient client, IOptions<OpenMeteoSettings> settings, TimeProvider time, ILogger<OpenMeteoService> logger)
    { _client = client; _settings = settings.Value; _time = time; _logger = logger; }

    public async Task<WeatherObservation> GetWeather(double latitude, double longitude, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled) throw new OpenMeteoException("Open-Meteo integration is disabled.");
        var query = BuildQuery(latitude, longitude, _settings.ForecastDays);
        for (var attempt = 0; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
            var timer = Stopwatch.StartNew();
            try
            {
                using var response = await _client.GetAsync(query, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                _logger.LogInformation("Open-Meteo HTTP {StatusCode} in {ElapsedMilliseconds} ms, attempt {Attempt}", (int)response.StatusCode, timer.ElapsedMilliseconds, attempt + 1);
                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? response.Headers.RetryAfter?.Date - _time.GetUtcNow();
                    var retryAt = retryAfter > TimeSpan.Zero ? _time.GetUtcNow().UtcDateTime.Add(retryAfter.Value) : (DateTime?)null;
                    if (attempt >= _settings.MaxRetryAttempts) throw new OpenMeteoException("Open-Meteo is temporarily unavailable.", retryAt);
                    /// :: A long provider cooldown is deferred to the next refresh instead of keeping a request open.
                    if (retryAfter > TimeSpan.FromMinutes(_settings.MinimumRefreshIntervalMinutes))
                        throw new OpenMeteoException("Open-Meteo requested a cooldown. Try again after the refresh interval.", retryAt);
                    await Delay(attempt, retryAfter, cancellationToken);
                    continue;
                }
                if (!response.IsSuccessStatusCode) throw new OpenMeteoException($"Open-Meteo request failed (HTTP {(int)response.StatusCode}).");
                if (response.Content.Headers.ContentLength > 2_097_152) throw new OpenMeteoException("Open-Meteo response exceeded the size limit.");
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var buffer = new MemoryStream();
                var bytes = new byte[8192];
                int count;
                while ((count = await stream.ReadAsync(bytes, timeout.Token)) > 0)
                {
                    if (buffer.Length + count > 2_097_152) throw new OpenMeteoException("Open-Meteo response exceeded the size limit.");
                    buffer.Write(bytes, 0, count);
                }
                buffer.Position = 0;
                using var json = await JsonDocument.ParseAsync(buffer, cancellationToken: timeout.Token);
                return OpenMeteoResponseParser.Parse(json.RootElement, _time.GetUtcNow().UtcDateTime);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < _settings.MaxRetryAttempts)
            { await Delay(attempt, null, cancellationToken); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { throw new OpenMeteoException("Open-Meteo request timed out."); }
            catch (HttpRequestException) when (attempt < _settings.MaxRetryAttempts)
            { await Delay(attempt, null, cancellationToken); }
            catch (HttpRequestException) { throw new OpenMeteoException("Open-Meteo is temporarily unreachable."); }
            catch (JsonException) { throw new OpenMeteoException("Open-Meteo returned invalid JSON."); }
        }
    }

    private Task Delay(int attempt, TimeSpan? retryAfter, CancellationToken cancellationToken) =>
        Task.Delay(retryAfter > TimeSpan.Zero ? retryAfter.Value : TimeSpan.FromSeconds(Math.Pow(2, attempt)), _time, cancellationToken);

    public static string BuildQuery(double latitude, double longitude, int days)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180 || days is < 1 or > 16)
            throw new OpenMeteoException("Provide valid coordinates and a forecast period between 1 and 16 days.");
        return $"forecast?latitude={latitude.ToString("R", CultureInfo.InvariantCulture)}&longitude={longitude.ToString("R", CultureInfo.InvariantCulture)}" +
            "&current=temperature_2m,relative_humidity_2m,apparent_temperature,wind_speed_10m,precipitation,cloud_cover,weather_code" +
            "&hourly=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation_probability,wind_speed_10m,cloud_cover,uv_index,shortwave_radiation" +
            $"&forecast_hours={Math.Min(days * 24 + 1, 384)}&timezone=UTC&timeformat=unixtime&temperature_unit=celsius&wind_speed_unit=kmh&precipitation_unit=mm";
    }
}
