namespace UrbanShade.Communication.Requests.ThermalAnalyses;

public sealed class GetThermalAnalysesRequest
{
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public string? RiskLevel { get; set; }
    public string? AlgorithmVersion { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
