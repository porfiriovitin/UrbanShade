using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.ProcessPending;
using UrbanShade.Domain.RegionThermalAnalysis;

namespace UrbanShade.Infraestructure.Workers;

public sealed class RegionThermalAnalysisBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly RegionThermalAnalysisSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<RegionThermalAnalysisBackgroundService> _logger;

    public RegionThermalAnalysisBackgroundService(IServiceScopeFactory scopes, RegionThermalAnalysisSettings settings, TimeProvider time, ILogger<RegionThermalAnalysisBackgroundService> logger)
    {
        _scopes = scopes;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled || !_settings.AutomaticRefreshEnabled) return;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), _time, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IProcessPendingRegionThermalAnalysesUseCase>().Execute(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Regional thermal analysis scheduling failed");
                }

                await Task.Delay(TimeSpan.FromMinutes(_settings.RefreshIntervalMinutes), _time, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
