using System.Net;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.RegionThermalAnalysis;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.ThermalAnalysis;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.Analyze;

public sealed class AnalyzeRegionThermalUseCase : IAnalyzeRegionThermalUseCase
{
    private readonly IRegionThermalAnalysisRepository _analyses;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RegionThermalAnalysisSettings _settings;
    private readonly GeoAnalysisSettings _geo;
    private readonly ThermalAnalysisSettings _thermal;
    private readonly ExposureIndexCalculator _calculator;
    private readonly HeatIndexCalculator _heatIndex;
    private readonly TimeProvider _time;
    private readonly ILogger<AnalyzeRegionThermalUseCase> _logger;

    public AnalyzeRegionThermalUseCase(IRegionThermalAnalysisRepository analyses, IUnitOfWork unitOfWork, RegionThermalAnalysisSettings settings,
        GeoAnalysisSettings geo, ThermalAnalysisSettings thermal, ExposureIndexCalculator calculator, HeatIndexCalculator heatIndex, TimeProvider time, ILogger<AnalyzeRegionThermalUseCase> logger)
    {
        _analyses = analyses;
        _unitOfWork = unitOfWork;
        _settings = settings;
        _geo = geo;
        _thermal = thermal;
        _calculator = calculator;
        _heatIndex = heatIndex;
        _time = time;
        _logger = logger;
    }

    public async Task<RegionThermalAnalysisResponse> Execute(Guid id, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        if (id == Guid.Empty) throw new RegionThermalAnalysisException(HttpStatusCode.BadRequest, "A region is required.");
        if (!_settings.Enabled) throw new RegionThermalAnalysisException(HttpStatusCode.Conflict, "Regional thermal analysis is disabled.");

        /// :: Serialize processing across API requests and background workers.
        await using var transaction = await _analyses.Acquire(id, cancellationToken)
            ?? throw new RegionThermalAnalysisException(HttpStatusCode.Conflict, "This region is already being analyzed.");
        var now = _time.GetUtcNow().UtcDateTime;
        var data = await _analyses.GetData(id, now, cancellationToken)
            ?? throw new RegionThermalAnalysisException(HttpStatusCode.NotFound, "Active region not found.");
        var heatIndex = data.Sensors.Count > 0 ? data.Sensors.Average(item => item.Analysis.HeatIndex)
            : data.Weather is not null ? _heatIndex.Calculate(data.Weather.Temperature!.Value, data.Weather.Humidity!.Value) : (decimal?)null;
        var center = data.Region.Boundary.EnvelopeInternal.Centre;
        var quadrants = data.Sensors.Select(item => (item.Sensor.Longitude >= center.X ? 1 : 0) + (item.Sensor.Latitude >= center.Y ? 2 : 0)).Distinct().Count();
        var pixelCoverage = new[] { data.Vegetation?.ValidPixelPercent, data.Surface?.ValidPixelPercent, data.ReferenceSurface?.ValidPixelPercent };
        var input = new ExposureInput(heatIndex, data.ReferenceSurface is null ? null : data.Surface!.MeanValue - data.ReferenceSurface.MeanValue,
            data.Vegetation?.VegetationCoveragePercent, data.Sensors.Count, quadrants, data.Sensors.Any(item => item.IsSimulated), pixelCoverage.Min());
        var result = _calculator.Calculate(input);

        /// :: Include eligibility and configuration so expiration and rule changes invalidate the previous result.
        var identity = JsonSerializer.Serialize(new
        {
            region = data.Region.Id, data.Region.BoundaryVersion, data.Region.ReferenceGeoRegionId,
            referenceVersion = data.ReferenceRegion?.BoundaryVersion,
            sensors = data.Sensors.Select(item => new { item.Sensor.Id, item.Sensor.Latitude, item.Sensor.Longitude, readingId = item.Reading.Id, analysisId = item.Analysis.Id, item.IsSimulated }),
            weather = data.Weather?.Id, vegetation = data.Vegetation?.Id, surface = data.Surface?.Id, referenceSurface = data.ReferenceSurface?.Id,
            input, settings = _settings, minimumPixels = _geo.MinimumValidPixelPercent, sensorValidity = _thermal.StaleDataThresholdMinutes, thermalVersion = _thermal.AlgorithmVersion
        });
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        var previous = await _analyses.GetLatest(id, null, cancellationToken);
        if (previous?.InputSignature == signature)
        {
            await transaction.Commit(cancellationToken);
            return RegionThermalAnalysisMapper.ToResponse(previous, data.Region.Name);
        }

        var metadata = JsonSerializer.Serialize(new
        {
            sensors = data.Sensors.Select(item => new { sensorId = item.Sensor.Id, readingId = item.Reading.Id, analysisId = item.Analysis.Id, item.Reading.MeasuredAt, item.Analysis.AlgorithmVersion, item.IsSimulated }),
            weather = data.Weather is null ? null : new { data.Weather.Id, data.Weather.ObservedAt, data.Weather.Source, data.Weather.Model, algorithmVersion = _thermal.AlgorithmVersion },
            vegetation = Satellite(data.Vegetation), surface = Satellite(data.Surface), referenceSurface = Satellite(data.ReferenceSurface),
            referenceRegionId = data.ReferenceRegion?.Id, referenceBoundaryVersion = data.ReferenceRegion?.BoundaryVersion,
            scores = result.Scores, appliedWeights = result.AppliedWeights,
            missingComponents = new[] { "Thermal", "SurfaceTemperature", "Vegetation" }.Except(result.Scores.Keys),
            occupiedQuadrants = quadrants, confidenceRules = _settings.Confidence, parameters = _settings,
            experimental = true, satelliteContext = "HISTORICAL_ENVIRONMENTAL_CONTEXT"
        });
        var analysis = new RegionThermalAnalysis(data, input, result, _settings.AlgorithmVersion, signature, metadata, now);
        _analyses.Add(analysis);
        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
        _logger.LogInformation("Analyzed region {RegionId} in {DurationMs} ms: sensors {SensorCount}, origin {DataOrigin}, index {ExposureIndex}, level {ExposureLevel}, quality {DataQuality}, version {AlgorithmVersion}, missing {MissingComponents}",
            id, timer.ElapsedMilliseconds, analysis.SensorCount, analysis.DataOrigin, analysis.ExposureIndex, analysis.ExposureLevel, analysis.DataQuality, analysis.AlgorithmVersion,
            string.Join(",", new[] { "Thermal", "SurfaceTemperature", "Vegetation" }.Except(result.Scores.Keys)));
        return RegionThermalAnalysisMapper.ToResponse(analysis, data.Region.Name);
    }

    private static object? Satellite(SatelliteAnalysis? item) => item is null ? null : new
    {
        item.Id, item.GeoRegionId, item.BoundaryVersion, source = item.DataSource.ToString(), item.ImageId, item.DatasetId,
        item.ObservationStartAt, item.ObservationEndAt, item.ValidPixelPercent, item.AlgorithmVersion
    };
}
