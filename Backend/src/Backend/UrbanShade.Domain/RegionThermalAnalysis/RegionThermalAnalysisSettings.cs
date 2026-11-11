namespace UrbanShade.Domain.RegionThermalAnalysis;

public sealed class RegionThermalAnalysisSettings
{
    public bool Enabled { get; set; } = true;
    public bool AutomaticRefreshEnabled { get; set; } = true;
    public int RefreshIntervalMinutes { get; set; } = 15;
    public string AlgorithmVersion { get; set; } = "1.0";
    public ExposureWeights Weights { get; set; } = new();
    public ExposureThresholds RiskThresholds { get; set; } = new();
    public HeatIslandThresholds HeatIslandDetection { get; set; } = new();
    public SourceValidity SourceValidity { get; set; } = new();
    public OperationalConfidence Confidence { get; set; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AlgorithmVersion) || AlgorithmVersion.Length > 32 || AlgorithmVersion != AlgorithmVersion.Trim() ||
            RefreshIntervalMinutes is < 1 or > 1440 || Weights is null || RiskThresholds is null || HeatIslandDetection is null || SourceValidity is null || Confidence is null)
            throw new InvalidOperationException("Invalid regional thermal analysis settings.");
        if (Weights.Thermal <= 0 || Weights.SurfaceTemperature < 0 || Weights.Vegetation < 0 ||
            Weights.Thermal + Weights.SurfaceTemperature + Weights.Vegetation != 1 || Weights.SurfaceTemperature + Weights.Vegetation <= 0)
            throw new InvalidOperationException("Exposure weights must total 1 and include thermal and environmental contributions.");
        if (RiskThresholds.Moderate <= 0 || RiskThresholds.Moderate >= RiskThresholds.High || RiskThresholds.High >= RiskThresholds.Critical || RiskThresholds.Critical > 100 ||
            HeatIslandDetection.MinimumSurfaceAnomaly <= 0 || HeatIslandDetection.MaximumVegetationCoverage is < 0 or > 100 ||
            SourceValidity.SensorMinutes is < 1 or > 525600 || SourceValidity.WeatherMinutes is < 1 or > 525600 ||
            SourceValidity.VegetationDays is < 1 or > 3650 || SourceValidity.SurfaceTemperatureDays is < 1 or > 3650 ||
            Confidence.MinimumSensorsForHigh < 3 || Confidence.MinimumOccupiedQuadrants is < 3 or > 4 || Confidence.MinimumValidPixelPercent is < 90 or > 100)
            throw new InvalidOperationException("Invalid exposure thresholds, source validity or confidence rules.");
    }
}

public sealed class ExposureWeights
{
    public decimal Thermal { get; set; } = 0.50m;
    public decimal SurfaceTemperature { get; set; } = 0.30m;
    public decimal Vegetation { get; set; } = 0.20m;
}

public sealed class ExposureThresholds
{
    public decimal Moderate { get; set; } = 25;
    public decimal High { get; set; } = 50;
    public decimal Critical { get; set; } = 75;
}

public sealed class HeatIslandThresholds
{
    public decimal MinimumSurfaceAnomaly { get; set; } = 3;
    public decimal MaximumVegetationCoverage { get; set; } = 30;
}

public sealed class SourceValidity
{
    public int SensorMinutes { get; set; } = 15;
    public int WeatherMinutes { get; set; } = 120;
    public int VegetationDays { get; set; } = 90;
    public int SurfaceTemperatureDays { get; set; } = 90;
}

public sealed class OperationalConfidence
{
    public int MinimumSensorsForHigh { get; set; } = 3;
    public int MinimumOccupiedQuadrants { get; set; } = 3;
    public decimal MinimumValidPixelPercent { get; set; } = 90;
}
