using System.Text.Json;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.RegionThermalAnalysis;

namespace UrbanShade.Domain.Routing;

public sealed class RouteExposureCalculator
{
    private readonly RoutingSettings _routing;
    private readonly RegionThermalAnalysisSettings _thermal;

    public RouteExposureCalculator(RoutingSettings routing, RegionThermalAnalysisSettings thermal)
    {
        _routing = routing;
        _thermal = thermal;
    }

    public RouteExposure Calculate(RouteCandidate route, IReadOnlyList<RouteThermalSegment> segments, DateTime now)
    {
        if (segments.Count == 0 || segments.Any(item => !double.IsFinite(item.DistanceMeters) || item.DistanceMeters <= 0))
            throw new ArgumentException("Route segments must have positive finite lengths.", nameof(segments));
        var evaluated = segments.Select(item => Evaluate(item, now)).ToList();
        var total = evaluated.Sum(item => item.Segment.DistanceMeters);
        var valid = evaluated.Where(item => item.IsValid).ToList();
        var covered = valid.Sum(item => item.Segment.DistanceMeters);
        var coverage = (decimal)(covered / total * 100);
        decimal? index = covered > 0 ? decimal.Round(valid.Sum(item => (decimal)item.Segment.DistanceMeters * item.Segment.ExposureIndex!.Value) / (decimal)covered, 2, MidpointRounding.AwayFromZero) : null;
        ExposureLevel? level = index is null ? null : index >= _thermal.RiskThresholds.Critical ? Enums.ExposureLevel.CRITICAL
            : index >= _thermal.RiskThresholds.High ? Enums.ExposureLevel.HIGH : index >= _thermal.RiskThresholds.Moderate ? Enums.ExposureLevel.MODERATE : Enums.ExposureLevel.LOW;
        var high = valid.Where(item => item.Segment.ExposureLevel is Enums.ExposureLevel.HIGH or Enums.ExposureLevel.CRITICAL).ToList();
        var compositions = valid.Select(item => item.Composition!).Distinct().Order().ToList();
        var eligible = coverage >= _routing.MinimumThermalCoveragePercent && compositions.Count == 1 && valid.All(item => item.Segment.ConfidenceLevel != Enums.ConfidenceLevel.LOW);
        var highCount = 0;
        Guid? lastRegion = null;
        var wasHigh = false;
        foreach (var item in evaluated)
        {
            var isHigh = item.IsValid && item.Segment.ExposureLevel is Enums.ExposureLevel.HIGH or Enums.ExposureLevel.CRITICAL;
            if (isHigh && (!wasHigh || lastRegion != item.Segment.GeoRegionId)) highCount++;
            wasHigh = isHigh;
            lastRegion = item.Segment.GeoRegionId;
        }
        var highDistance = high.Sum(item => item.Segment.DistanceMeters);
        return new(route, evaluated, total, index, level, coverage, highDistance, decimal.Round((decimal)(highDistance / total * 100), 2), highCount,
            eligible, string.Join("|", compositions), valid.Any(item => item.Segment.DataOrigin == "SIMULATED_SENSORS"),
            covered == 0 ? "INSUFFICIENT_DATA" : evaluated.Any(item => !item.IsValid || item.Segment.DataQuality != RegionDataQuality.COMPLETE) ? "PARTIAL" : "COMPLETE");
    }

    private EvaluatedRouteSegment Evaluate(RouteThermalSegment segment, DateTime now)
    {
        if (!_thermal.Enabled || segment.ExposureIndex is null or < 0 or > 100 || segment.DataQuality is null or RegionDataQuality.INSUFFICIENT_DATA ||
            segment.AlgorithmVersion != _thermal.AlgorithmVersion || segment.AnalyzedAt is null || segment.AnalyzedAt > now || segment.SourceMetadata is null)
            return new(segment, false, null, null);
        try
        {
            using var document = JsonDocument.Parse(segment.SourceMetadata);
            var root = document.RootElement;
            var scores = root.GetProperty("scores");
            var weights = root.GetProperty("appliedWeights");
            var parameters = JsonSerializer.Deserialize<RegionThermalAnalysisSettings>(root.GetProperty("parameters"));
            if (parameters is null || JsonSerializer.Serialize(parameters.Weights) != JsonSerializer.Serialize(_thermal.Weights) ||
                JsonSerializer.Serialize(parameters.RiskThresholds) != JsonSerializer.Serialize(_thermal.RiskThresholds)) return new(segment, false, null, null);
            var dates = new List<DateTime>();
            if (!scores.TryGetProperty("Thermal", out _)) return new(segment, false, null, null);
            var sensors = root.GetProperty("sensors");
            if (sensors.GetArrayLength() > 0)
            {
                foreach (var sensor in sensors.EnumerateArray())
                    if (!Current(sensor.GetProperty("MeasuredAt").GetDateTime(), TimeSpan.FromMinutes(_thermal.SourceValidity.SensorMinutes))) return new(segment, false, null, null);
            }
            else if (!Current(root.GetProperty("weather").GetProperty("ObservedAt").GetDateTime(), TimeSpan.FromMinutes(_thermal.SourceValidity.WeatherMinutes))) return new(segment, false, null, null);
            var environmental = false;
            if (scores.TryGetProperty("Vegetation", out _))
            {
                environmental = true;
                if (!Current(root.GetProperty("vegetation").GetProperty("ObservationEndAt").GetDateTime(), TimeSpan.FromDays(_thermal.SourceValidity.VegetationDays))) return new(segment, false, null, null);
            }
            if (scores.TryGetProperty("SurfaceTemperature", out _))
            {
                environmental = true;
                if (!Current(root.GetProperty("surface").GetProperty("ObservationEndAt").GetDateTime(), TimeSpan.FromDays(_thermal.SourceValidity.SurfaceTemperatureDays)) ||
                    !Current(root.GetProperty("referenceSurface").GetProperty("ObservationEndAt").GetDateTime(), TimeSpan.FromDays(_thermal.SourceValidity.SurfaceTemperatureDays))) return new(segment, false, null, null);
            }
            if (!environmental) return new(segment, false, null, null);
            var composition = string.Join(",", weights.EnumerateObject().OrderBy(item => item.Name).Select(item => item.Name + ":" + item.Value.GetRawText()));
            return new(segment, true, composition, dates.Min());

            bool Current(DateTime date, TimeSpan validity)
            {
                dates.Add(date);
                return date.Kind == DateTimeKind.Utc && date <= now && now - date <= validity;
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { return new(segment, false, null, null); }
    }
}
