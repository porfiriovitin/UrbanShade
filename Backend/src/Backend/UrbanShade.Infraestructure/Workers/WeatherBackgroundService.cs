using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrbanShade.Application.Services;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Weather;
using UrbanShade.Exceptions;

namespace UrbanShade.Infraestructure.Workers;

public sealed class WeatherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly WeatherSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<WeatherBackgroundService> _logger;

    public WeatherBackgroundService(IServiceScopeFactory scopes, IOptions<WeatherSettings> settings, TimeProvider time, ILogger<WeatherBackgroundService> logger)
    {
        _scopes = scopes;
        _settings = settings.Value;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled || !_settings.AutomaticRefreshEnabled) return;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), _time, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshDueRegions(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Weather scheduling failed");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), _time, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public async Task RefreshDueRegions(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        List<Guid> regions;
        await using (var scope = _scopes.CreateAsyncScope())
            regions = await scope.ServiceProvider.GetRequiredService<IWeatherRepository>().GetDueRegions(now, cancellationToken);

        foreach (var regionId in regions)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<WeatherService>().Refresh(regionId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (WeatherException exception) when (exception.GetErrorKind() is ErrorKind.Conflict or ErrorKind.RateLimited or ErrorKind.NotFound)
            {
                _logger.LogInformation("Skipped weather refresh for region {RegionId}: {Reason}", regionId, exception.GetErrorMessages()[0]);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Weather refresh failed for region {RegionId}; continuing with other regions", regionId);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), _time, cancellationToken);
        }
    }
}
