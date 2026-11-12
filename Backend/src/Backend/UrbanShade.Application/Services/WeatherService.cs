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
    private readonly IWeatherProvider _provider;
    private readonly WeatherSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<WeatherService> _logger;
    public WeatherService(IGeoRegionRepository regions, IWeatherRepository weather, IWeatherProvider provider,
        IOptions<WeatherSettings> settings, TimeProvider time, ILogger<WeatherService> logger)
    { _regions = regions; _weather = weather; _provider = provider; _settings = settings.Value; _time = time; _logger = logger; }

    public async Task Refresh(Guid regionId, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled) throw new WeatherException(ErrorKind.Unavailable, "Weather integration is disabled.");
        var existingRegion = await _regions.FindIncludingInactive(regionId, cancellationToken);
        if (existingRegion is null || !existingRegion.Active) throw new WeatherException(ErrorKind.NotFound, "Active region not found.");
        await using var refreshLock = await _weather.AcquireRefresh(regionId, cancellationToken);
        if (refreshLock is null) throw new WeatherException(ErrorKind.Conflict, "A weather refresh is already running for this region.");
        var region = await _regions.FindIncludingInactive(regionId, cancellationToken);
        if (region is null || !region.Active) throw new WeatherException(ErrorKind.NotFound, "Active region not found.");
        var now = _time.GetUtcNow().UtcDateTime;
        if (!await _weather.RequestRefresh(regionId, refreshLock.Id, region.BoundaryVersion, now.AddMinutes(-_settings.MinimumRefreshIntervalMinutes), now, cancellationToken))
            throw new WeatherException(ErrorKind.RateLimited, "Wait for the minimum weather refresh interval before trying again.");
        _logger.LogInformation("Starting weather refresh for region {RegionId}, boundary version {BoundaryVersion}", regionId, region.BoundaryVersion);
        try
        {
            using var providerTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            providerTimeout.CancelAfter(TimeSpan.FromMinutes(8));
            var point = await _regions.GetWeatherPoint(regionId, cancellationToken);
            var observation = await _provider.GetWeather(point.Y, point.X, providerTimeout.Token);
            if (!await _weather.Save(region, refreshLock.Id, point.Y, point.X, observation, cancellationToken))
                throw new WeatherException(ErrorKind.Conflict, "The region changed during the weather refresh. Previous data was preserved.");
            _logger.LogInformation("Completed weather refresh for region {RegionId}: {ForecastCount} forecasts, fetched at {FetchedAt}", regionId, observation.Hourly.Count, observation.FetchedAt);
        }
        catch (WeatherProviderException exception)
        {
            if (exception.RetryAt.HasValue)
                await _weather.DeferRefresh(regionId, refreshLock.Id, exception.RetryAt.Value, cancellationToken);
            _logger.LogWarning("Weather provider failed for region {RegionId}: {Reason}", regionId, exception.Message);
            throw new WeatherException(ErrorKind.Unavailable, exception.Message, "Previous weather data was preserved.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (WeatherException) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Weather refresh failed for region {RegionId}; previous data was preserved", regionId);
            throw new WeatherException(ErrorKind.Unavailable, "Weather refresh failed. Previous data was preserved.");
        }
    }
}
