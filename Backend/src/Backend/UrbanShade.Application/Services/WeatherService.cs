using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Weather;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.Services;

public sealed class WeatherService
{
    private readonly IGeoRegionRepository _regions;
    private readonly IWeatherRepository _weather;
    private readonly IOpenMeteoService _provider;
    private readonly OpenMeteoSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<WeatherService> _logger;
    public WeatherService(IGeoRegionRepository regions, IWeatherRepository weather, IOpenMeteoService provider,
        IOptions<OpenMeteoSettings> settings, TimeProvider time, ILogger<WeatherService> logger)
    { _regions = regions; _weather = weather; _provider = provider; _settings = settings.Value; _time = time; _logger = logger; }

    public async Task Refresh(Guid regionId, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled) throw new WeatherException(HttpStatusCode.ServiceUnavailable, "Open-Meteo integration is disabled.");
        await using var refreshLock = await _weather.AcquireRefresh(regionId, cancellationToken);
        if (refreshLock is null) throw new WeatherException(HttpStatusCode.Conflict, "A weather refresh is already running for this region.");
        var region = await _regions.FindIncludingInactive(regionId, cancellationToken);
        if (region is null || !region.Active) throw new WeatherException(HttpStatusCode.NotFound, "Active region not found.");
        var now = _time.GetUtcNow().UtcDateTime;
        if (!await _weather.RequestRefresh(regionId, region.BoundaryVersion, now.AddMinutes(-_settings.MinimumRefreshIntervalMinutes), now, cancellationToken))
            throw new WeatherException(HttpStatusCode.TooManyRequests, "Wait for the minimum weather refresh interval before trying again.");
        _logger.LogInformation("Starting weather refresh for region {RegionId}, boundary version {BoundaryVersion}", regionId, region.BoundaryVersion);
        try
        {
            var point = await _regions.GetWeatherPoint(regionId, cancellationToken);
            var observation = await _provider.GetWeather(point.Y, point.X, cancellationToken);
            if (!await _weather.Save(region, point.Y, point.X, observation, cancellationToken))
                throw new WeatherException(HttpStatusCode.Conflict, "The region changed during the weather refresh. Previous data was preserved.");
            _logger.LogInformation("Completed weather refresh for region {RegionId}: {ForecastCount} forecasts, fetched at {FetchedAt}", regionId, observation.Hourly.Count, observation.FetchedAt);
        }
        catch (OpenMeteoException exception)
        {
            if (exception.RetryAt.HasValue)
                await _weather.DeferRefresh(regionId, exception.RetryAt.Value, cancellationToken);
            _logger.LogWarning("Weather provider failed for region {RegionId}: {Reason}", regionId, exception.Message);
            throw new WeatherException(HttpStatusCode.ServiceUnavailable, exception.Message, "Previous weather data was preserved.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (WeatherException) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Weather refresh failed for region {RegionId}; previous data was preserved", regionId);
            throw new WeatherException(HttpStatusCode.ServiceUnavailable, "Weather refresh failed. Previous data was preserved.");
        }
    }
}
