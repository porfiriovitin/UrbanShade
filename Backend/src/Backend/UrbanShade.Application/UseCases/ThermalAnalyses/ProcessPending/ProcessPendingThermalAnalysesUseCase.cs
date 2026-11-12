using Microsoft.Extensions.Logging;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.ThermalAnalysis;

namespace UrbanShade.Application.UseCases.ThermalAnalyses.ProcessPending;

public sealed class ProcessPendingThermalAnalysesUseCase : IProcessPendingThermalAnalysesUseCase
{
    private readonly IThermalAnalysisRepository _analyses;
    private readonly HeatIndexCalculator _calculator;
    private readonly ThermalAnalysisSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<ProcessPendingThermalAnalysesUseCase> _logger;

    public ProcessPendingThermalAnalysesUseCase(IThermalAnalysisRepository analyses, HeatIndexCalculator calculator, ThermalAnalysisSettings settings, TimeProvider time, ILogger<ProcessPendingThermalAnalysesUseCase> logger)
    {
        _analyses = analyses;
        _calculator = calculator;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return;

        List<Guid> failedReadings = [];

        while (!cancellationToken.IsCancellationRequested)
        {
            var readings = await _analyses.GetPending(_settings.AlgorithmVersion, 100, failedReadings, cancellationToken);
            if (readings.Count == 0) return;

            foreach (var reading in readings)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var heatIndex = _calculator.Calculate(reading.Temperature, reading.Humidity);
                    var analysis = new ThermalAnalysis(reading.Id, heatIndex, _settings.Classify(heatIndex), _settings.AlgorithmVersion, _time.GetUtcNow().UtcDateTime);
                    await _analyses.TryAddForActiveSensor(analysis, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    /// :: Retry failures next cycle; continue later batches.
                    failedReadings.Add(reading.Id);
                    _logger.LogError(exception, "Failed to analyze reading {SensorReadingId} from sensor {SensorId} using algorithm {AlgorithmVersion}.", reading.Id, reading.SensorId, _settings.AlgorithmVersion);
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
