namespace UrbanShade.Domain.Geospatial;

public sealed class GeoAnalysisSettings
{
    public bool Enabled { get; set; }
    public bool AutomaticRefreshEnabled { get; set; }
    public int RefreshIntervalDays { get; set; } = 7;
    public int ObservationWindowDays { get; set; } = 60;
    public int MaximumPeriodDays { get; set; } = 366;
    public int MaximumVertices { get; set; } = 2000;
    public double MaximumRegionAreaSquareMeters { get; set; } = 100_000_000;
    public int OutdatedAfterDays { get; set; } = 60;
    public double VegetationNdviThreshold { get; set; } = 0.4;
    public double MinimumValidPixelPercent { get; set; } = 70;
    public int MaxCloudProbability { get; set; } = 40;
    public double MinimumGreenAreaSquareMeters { get; set; } = 500;
    public int MaximumGreenAreas { get; set; } = 200;
    public string AlgorithmVersion { get; set; } = "geo-v1";

    public GeoProcessingParameters Parameters() => new(VegetationNdviThreshold, MinimumValidPixelPercent, MaxCloudProbability, MinimumGreenAreaSquareMeters, MaximumGreenAreas);

    public void Validate()
    {
        if (RefreshIntervalDays < 1 || ObservationWindowDays < 1 || MaximumPeriodDays < ObservationWindowDays || MaximumPeriodDays > 3660 || MaximumVertices < 4 || MaximumRegionAreaSquareMeters <= 0 || OutdatedAfterDays < 1 || !double.IsFinite(VegetationNdviThreshold) || VegetationNdviThreshold is < -1 or > 1 || !double.IsFinite(MinimumValidPixelPercent) || MinimumValidPixelPercent is <= 0 or > 100 || MaxCloudProbability is < 0 or > 100 || MinimumGreenAreaSquareMeters <= 0 || MaximumGreenAreas is < 1 or > 1000 || string.IsNullOrWhiteSpace(AlgorithmVersion) || AlgorithmVersion.Length > 32)
            throw new InvalidOperationException("Invalid geospatial processing settings.");
    }
}
