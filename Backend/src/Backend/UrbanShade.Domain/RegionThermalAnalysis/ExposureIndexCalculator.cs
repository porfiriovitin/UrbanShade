using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.RegionThermalAnalysis;

public sealed class ExposureIndexCalculator
{
    private readonly RegionThermalAnalysisSettings _settings;

    public ExposureIndexCalculator(RegionThermalAnalysisSettings settings)
    {
        _settings = settings;
    }

    public ExposureResult Calculate(ExposureInput input)
    {
        if (input.VegetationCoveragePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(input));

        /// :: Normalize each component before combining different physical quantities.
        decimal? thermal = input.HeatIndex.HasValue ? Math.Clamp((input.HeatIndex.Value - 25) / 20 * 100, 0, 100) : null;
        decimal? surface = input.SurfaceAnomaly.HasValue ? Math.Clamp((input.SurfaceAnomaly.Value + 2) / 10 * 100, 0, 100) : null;
        decimal? vegetation = input.VegetationCoveragePercent.HasValue ? 100 - input.VegetationCoveragePercent.Value : null;
        bool? candidate = surface.HasValue && vegetation.HasValue
            ? input.SurfaceAnomaly >= _settings.HeatIslandDetection.MinimumSurfaceAnomaly && input.VegetationCoveragePercent < _settings.HeatIslandDetection.MaximumVegetationCoverage
            : null;

        var scores = new Dictionary<string, decimal>();
        var weights = new Dictionary<string, decimal>();
        Add("Thermal", thermal, _settings.Weights.Thermal);
        Add("SurfaceTemperature", surface, _settings.Weights.SurfaceTemperature);
        Add("Vegetation", vegetation, _settings.Weights.Vegetation);

        /// :: A current thermal source and a contributing environmental source are mandatory.
        if (!thermal.HasValue || (!surface.HasValue && !vegetation.HasValue) || !weights.ContainsKey("Thermal") || weights.Keys.All(key => key == "Thermal"))
            return new(null, null, RegionDataQuality.INSUFFICIENT_DATA, ConfidenceLevel.LOW, candidate, scores, new());

        var totalWeight = weights.Values.Sum();
        var appliedWeights = weights.ToDictionary(item => item.Key, item => item.Value / totalWeight);
        var index = decimal.Round(appliedWeights.Sum(item => scores[item.Key] * item.Value), 2, MidpointRounding.AwayFromZero);
        var level = index >= _settings.RiskThresholds.Critical ? ExposureLevel.CRITICAL
            : index >= _settings.RiskThresholds.High ? ExposureLevel.HIGH
            : index >= _settings.RiskThresholds.Moderate ? ExposureLevel.MODERATE : ExposureLevel.LOW;
        var quality = thermal.HasValue && surface.HasValue && vegetation.HasValue ? RegionDataQuality.COMPLETE : RegionDataQuality.PARTIAL;
        var confidence = input.SensorCount == 1 ? ConfidenceLevel.LOW : ConfidenceLevel.MEDIUM;

        /// :: HIGH is an operational coverage rule, never a statistical accuracy estimate.
        if (quality == RegionDataQuality.COMPLETE && !input.HasSimulatedSensors &&
            input.SensorCount >= _settings.Confidence.MinimumSensorsForHigh &&
            input.OccupiedQuadrants >= _settings.Confidence.MinimumOccupiedQuadrants &&
            input.MinimumValidPixelPercent >= _settings.Confidence.MinimumValidPixelPercent)
            confidence = ConfidenceLevel.HIGH;

        return new(index, level, quality, confidence, candidate, scores, appliedWeights);

        void Add(string name, decimal? score, decimal weight)
        {
            if (!score.HasValue) return;
            scores.Add(name, score.Value);
            if (weight > 0) weights.Add(name, weight);
        }
    }
}

public sealed record ExposureResult(decimal? Index, ExposureLevel? Level, RegionDataQuality Quality,
    ConfidenceLevel Confidence, bool? IsHeatIslandCandidate, Dictionary<string, decimal> Scores, Dictionary<string, decimal> AppliedWeights);
