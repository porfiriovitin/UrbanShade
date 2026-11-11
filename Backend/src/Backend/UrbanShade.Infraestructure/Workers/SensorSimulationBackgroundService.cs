using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UrbanShade.Application.UseCases.SensorReadings.Generate;
using UrbanShade.Domain.Simulation;

namespace UrbanShade.Infraestructure.Workers;

public sealed class SensorSimulationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly SensorSimulationSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<SensorSimulationBackgroundService> _logger;

    public SensorSimulationBackgroundService(IServiceScopeFactory scopes, SensorSimulationSettings settings, TimeProvider time, ILogger<SensorSimulationBackgroundService> logger)
    {
        _scopes = scopes;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                /// :: Resolve a fresh context for every cycle, including retries after failures.
                await using var scope = _scopes.CreateAsyncScope();
                var useCase = scope.ServiceProvider.GetRequiredService<IGenerateSensorReadingsUseCase>();
                await useCase.Execute(stoppingToken);
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
                await Task.Delay(TimeSpan.FromMinutes(_settings.IntervalMinutes), _time, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
