using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UrbanShade.Application.UseCases.GreenAreas.Import;
using UrbanShade.Application.UseCases.GreenAreas.Refresh;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.Workers;

public sealed class OpenStreetMapBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly OpenStreetMapSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<OpenStreetMapBackgroundService> _logger;

    public OpenStreetMapBackgroundService(IServiceScopeFactory scopes, OpenStreetMapSettings settings, TimeProvider time, ILogger<OpenStreetMapBackgroundService> logger)
    {
        _scopes = scopes;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled) return;
        var nextSchedule = DateTime.MinValue;
        try { await Task.Delay(TimeSpan.FromSeconds(10), _time, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_settings.AutomaticRefreshEnabled && _time.GetUtcNow().UtcDateTime >= nextSchedule)
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    var jobs = scope.ServiceProvider.GetRequiredService<IGeoAnalysisJobRepository>();
                    var refresh = scope.ServiceProvider.GetRequiredService<IRefreshOpenStreetMapUseCase>();
                    var regions = await jobs.GetDueRegions(_time.GetUtcNow().UtcDateTime.AddDays(-_settings.RefreshIntervalDays), 10, stoppingToken, GeoJobType.OpenStreetMap);
                    foreach (var region in regions) await refresh.Execute(region.Id, stoppingToken);
                    nextSchedule = _time.GetUtcNow().UtcDateTime.AddMinutes(5);
                }
                await using var importScope = _scopes.CreateAsyncScope();
                await importScope.ServiceProvider.GetRequiredService<IImportOpenStreetMapUseCase>().Execute(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { _logger.LogError("OpenStreetMap worker failed with {ExceptionType}", exception.GetType().Name); }
            /// :: Space public Overpass requests, including when multiple jobs are waiting.
            try { await Task.Delay(TimeSpan.FromSeconds(10), _time, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
