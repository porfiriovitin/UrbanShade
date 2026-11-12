using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UrbanShade.Application.UseCases.SensorReadings.Generate;
using UrbanShade.Domain.Simulation;
using UrbanShade.Domain.ThermalAnalysis;
using UrbanShade.Application.UseCases.ThermalAnalyses.ProcessPending;

namespace UrbanShade.Infraestructure.Workers;

public sealed class SensorSimulationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly SensorSimulationSettings _settings;
    private readonly ThermalAnalysisSettings _thermal;
    private readonly TimeProvider _time;
    private readonly ILogger<SensorSimulationBackgroundService> _logger;

    public SensorSimulationBackgroundService(IServiceScopeFactory scopes, SensorSimulationSettings settings, ThermalAnalysisSettings thermal, TimeProvider time, ILogger<SensorSimulationBackgroundService> logger)
    {
        _scopes = scopes;
        _settings = settings;
        _thermal = thermal;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled && !_thermal.Enabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                /// :: Use a fresh context for each cycle.
                if (_settings.Enabled)
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    var useCase = scope.ServiceProvider.GetRequiredService<IGenerateSensorReadingsUseCase>();
                    await useCase.Execute(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to generate simulated sensor readings.");
            }

            try
            {
                if (_thermal.Enabled)
                {
                    /// :: Process committed readings in a separate context.
                    await using var scope = _scopes.CreateAsyncScope();
                    var useCase = scope.ServiceProvider.GetRequiredService<IProcessPendingThermalAnalysesUseCase>();
                    await useCase.Execute(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to process pending thermal analyses.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_settings.IntervalMinutes), _time, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
