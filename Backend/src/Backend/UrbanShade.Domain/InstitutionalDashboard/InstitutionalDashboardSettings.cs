namespace UrbanShade.Domain.InstitutionalDashboard;

public sealed class InstitutionalDashboardSettings
{
    public bool Enabled { get; set; } = true;
    public int OverviewCacheMinutes { get; set; } = 5;
    public int DistributionCacheMinutes { get; set; } = 5;
    public int RankingCacheMinutes { get; set; } = 10;
    public int EnvironmentCacheMinutes { get; set; } = 30;
    public int HistoryCacheMinutes { get; set; } = 15;
    public int DefaultPageSize { get; set; } = 20;
    public int MaxPageSize { get; set; } = 100;
    public int MaxHistoryDays { get; set; } = 365;
    public int MaxExportRows { get; set; } = 10000;
    public int MaxMapFeatures { get; set; } = 1000;
    public double MaxMapSpanDegrees { get; set; } = 2;
    public int MaxMapCoordinates { get; set; } = 100000;
    public decimal LowVegetationCoveragePercent { get; set; } = 30;
    public decimal PrioritySurfaceAnomaly { get; set; } = 3;

    public void Validate()
    {
        if (new[] { OverviewCacheMinutes, DistributionCacheMinutes, RankingCacheMinutes, EnvironmentCacheMinutes, HistoryCacheMinutes }.Any(value => value is < 1 or > 1440) || DefaultPageSize < 1 || DefaultPageSize > MaxPageSize || MaxPageSize > 1000 || MaxHistoryDays is < 1 or > 3650 || MaxExportRows is < 1 or > 100000 || MaxMapFeatures is < 1 or > 10000 || MaxMapCoordinates is < 100 or > 1000000 || !double.IsFinite(MaxMapSpanDegrees) || MaxMapSpanDegrees is <= 0 or > 10 || LowVegetationCoveragePercent is < 0 or > 100 || PrioritySurfaceAnomaly <= 0)
            throw new InvalidOperationException("Invalid institutional dashboard settings.");
    }
}
