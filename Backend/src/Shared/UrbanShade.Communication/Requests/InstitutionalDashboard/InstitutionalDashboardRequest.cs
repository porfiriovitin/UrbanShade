namespace UrbanShade.Communication.Requests.InstitutionalDashboard;

public sealed class InstitutionalDashboardRequest
{
    public Guid OrganizationId { get; init; }
    public Guid? RegionId { get; init; }
    public Guid[] RegionIds { get; init; } = [];
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? ExposureLevel { get; init; }
    public decimal? MinExposureIndex { get; init; }
    public string Sort { get; init; } = "DESC";
    public int Page { get; init; } = 1;
    public int? PageSize { get; init; }
    public int Limit { get; init; } = 10;
    public string Metric { get; init; } = "EXPOSURE_INDEX";
    public string Interval { get; init; } = "DAY";
    public string? Bbox { get; init; }
    public bool IncludeSensors { get; init; }
    public bool IncludeGreenAreas { get; init; }
    public string Format { get; init; } = "csv";
}
