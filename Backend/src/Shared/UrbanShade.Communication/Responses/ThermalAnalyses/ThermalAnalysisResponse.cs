namespace UrbanShade.Communication.Responses.ThermalAnalyses;

public class ThermalAnalysisResponse
{
    public Guid? Id { get; init; }
    public Guid SensorId { get; init; }
    public Guid? SensorReadingId { get; init; }
    public decimal? Temperature { get; init; }
    public decimal? Humidity { get; init; }
    public decimal? HeatIndex { get; init; }
    public string? RiskLevel { get; init; }
    public string? AlgorithmVersion { get; init; }
    public DateTime? MeasuredAt { get; init; }
    public DateTime? AnalyzedAt { get; init; }
    public string DataStatus { get; init; } = string.Empty;
}
