using NetTopologySuite.Geometries;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Entities;

public class GeoAnalysisJob : EntityBase
{
    private GeoAnalysisJob() { }
    public GeoAnalysisJob(GeoRegion region, DateTime start, DateTime end, string algorithmVersion, string parameters, GeoJobType jobType = GeoJobType.Satellite)
    {
        JobType = jobType;
        GeoRegionId = region.Id;
        Boundary = (Geometry)region.Boundary.Copy();
        BoundaryVersion = region.BoundaryVersion;
        ObservationStartAt = start;
        ObservationEndAt = end;
        AlgorithmVersion = algorithmVersion;
        Parameters = parameters;
    }
    public GeoJobType JobType { get; private set; }
    public Guid GeoRegionId { get; private set; }
    private Geometry _boundary = null!;
    public Geometry Boundary { get => (Geometry)_boundary.Copy(); private set => _boundary = (Geometry)value.Copy(); }
    public int BoundaryVersion { get; private set; }
    public DateTime ObservationStartAt { get; private set; }
    public DateTime ObservationEndAt { get; private set; }
    public string AlgorithmVersion { get; private set; } = string.Empty;
    public string Parameters { get; private set; } = string.Empty;
    public GeoJobStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    public Guid? LeaseId { get; private set; }
    public int Attempts { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ResultSummary { get; private set; }
    public void Complete(string summary)
    {
        Status = GeoJobStatus.Completed;
        FinishedAt = DateTime.UtcNow;
        LeaseExpiresAt = null;
        ResultSummary = summary;
        ErrorMessage = null;
    }
    public void Fail(string message)
    {
        Status = GeoJobStatus.Failed;
        FinishedAt = DateTime.UtcNow;
        LeaseExpiresAt = null;
        ErrorMessage = message;
    }
    public void Retry()
    {
        Status = GeoJobStatus.Pending;
        FinishedAt = null;
        ErrorMessage = null;
    }
}
