namespace UrbanShade.Communication.Requests.RegionThermalAnalyses;

public sealed class RegionThermalQueryRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ExposureLevel { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int Limit { get; set; } = 100;
    public decimal? MinExposureIndex { get; set; }
    public bool IncludePartial { get; set; } = true;
    public string? Bbox { get; set; }
    public string? Ids { get; set; }
}

