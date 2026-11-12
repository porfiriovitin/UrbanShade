using UrbanShade.Communication.Responses.ThermalAnalyses;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.ThermalAnalysis;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.ThermalAnalyses.GetLatest;

public sealed class GetLatestThermalAnalysisUseCase : IGetLatestThermalAnalysisUseCase
{
    private readonly ISensorRepository _sensors;
    private readonly IThermalAnalysisRepository _analyses;
    private readonly ThermalAnalysisSettings _settings;
    private readonly TimeProvider _time;

    public GetLatestThermalAnalysisUseCase(ISensorRepository sensors, IThermalAnalysisRepository analyses, ThermalAnalysisSettings settings, TimeProvider time)
    {
        _sensors = sensors;
        _analyses = analyses;
        _settings = settings;
        _time = time;
    }

    public async Task<ThermalAnalysisResponse> Execute(Guid sensorId, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        var sensor = await _sensors.FindById(sensorId, cancellationToken);
        if (sensor is null || (!isAdministrator && (!sensor.Active || sensor.Status != SensorStatus.ACTIVE)))
            throw new SensorException(ErrorKind.NotFound, "Sensor is unavailable.");

        var latest = await _analyses.GetLatest(sensorId, _settings.AlgorithmVersion, cancellationToken);

        return ToResponse(sensorId, latest, _time.GetUtcNow().UtcDateTime, _settings);
    }

    internal static ThermalAnalysisResponse ToResponse(Guid sensorId, (SensorReading Reading, ThermalAnalysis Analysis)? item, DateTime now, ThermalAnalysisSettings settings) => new()
    {
        Id = item?.Analysis.Id,
        SensorId = sensorId,
        SensorReadingId = item?.Reading.Id,
        Temperature = item?.Reading.Temperature,
        Humidity = item?.Reading.Humidity,
        HeatIndex = item?.Analysis.HeatIndex,
        RiskLevel = item?.Analysis.RiskLevel.ToString(),
        AlgorithmVersion = item?.Analysis.AlgorithmVersion,
        MeasuredAt = item?.Reading.MeasuredAt,
        AnalyzedAt = item?.Analysis.AnalyzedAt,
        DataStatus = item is null ? nameof(ThermalDataStatus.NO_DATA) : now - item.Value.Reading.MeasuredAt > TimeSpan.FromMinutes(settings.StaleDataThresholdMinutes) ? nameof(ThermalDataStatus.STALE) : nameof(ThermalDataStatus.CURRENT)
    };
}
