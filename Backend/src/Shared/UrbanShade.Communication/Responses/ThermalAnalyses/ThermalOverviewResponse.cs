namespace UrbanShade.Communication.Responses.ThermalAnalyses;

public sealed class ThermalOverviewResponse
{
    public int TotalActiveSensors { get; init; }
    public int SensorsWithCurrentData { get; init; }
    public decimal? AverageTemperature { get; init; }
    public decimal? AverageHeatIndex { get; init; }
    public decimal? HighestHeatIndex { get; init; }
    public ThermalRiskDistributionResponse RiskDistribution { get; init; } = new();
}

public sealed class ThermalRiskDistributionResponse
{
    public int Low { get; init; }
    public int Moderate { get; init; }
    public int High { get; init; }
    public int Critical { get; init; }
}
