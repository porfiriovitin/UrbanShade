using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UrbanShade.Application.UseCases.GeoAnalysisJobs.ProcessPending;
using UrbanShade.Application.UseCases.GeoAnalysisJobs.Refresh;
using UrbanShade.Communication.Requests.Geo;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.Workers;

public sealed class GeoAnalysisBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly GeoAnalysisSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<GeoAnalysisBackgroundService> _logger;
    public GeoAnalysisBackgroundService(IServiceScopeFactory scopes, GeoAnalysisSettings settings, TimeProvider time, ILogger<GeoAnalysisBackgroundService> logger)
    { _scopes = scopes; _settings = settings; _time = time; _logger = logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled) return;
        /// :: Let startup migrations finish before reading the durable queue.
        await Task.Delay(TimeSpan.FromSeconds(10), _time, stoppingToken);
        var nextSchedule = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_settings.AutomaticRefreshEnabled && _time.GetUtcNow().UtcDateTime >= nextSchedule)
                {
                    await using var scheduleScope = _scopes.CreateAsyncScope();
                    var jobs = scheduleScope.ServiceProvider.GetRequiredService<IGeoAnalysisJobRepository>();
                    var refresh = scheduleScope.ServiceProvider.GetRequiredService<IRefreshGeoRegionUseCase>();
                    var regions = await jobs.GetDueRegions(_time.GetUtcNow().UtcDateTime.AddDays(-_settings.RefreshIntervalDays), 20, stoppingToken);
                    foreach (var region in regions) await refresh.Execute(region.Id, new RefreshGeoRegionRequest(), stoppingToken);
                    nextSchedule = _time.GetUtcNow().UtcDateTime.AddMinutes(5);
                }
                await using var scope = _scopes.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IProcessPendingGeoAnalysesUseCase>();
                if (await processor.Execute(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                /// :: Log the exception type, never upstream response bodies or authentication details.
                _logger.LogError("Geospatial worker failed with {ExceptionType}", exception.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), _time, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
