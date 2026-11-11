using System.Net;
using System.Text.Json;
using UrbanShade.Communication.Requests.Geo;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GeoAnalysisJobs.Refresh;

public sealed class RefreshGeoRegionUseCase : IRefreshGeoRegionUseCase
{
    private readonly IGeoRegionRepository _regions;
    private readonly IGeoAnalysisJobRepository _jobs;
    private readonly GeoAnalysisSettings _settings;
    private readonly TimeProvider _time;
    public RefreshGeoRegionUseCase(IGeoRegionRepository regions, IGeoAnalysisJobRepository jobs, GeoAnalysisSettings settings, TimeProvider time)
    { _regions = regions; _jobs = jobs; _settings = settings; _time = time; }
    public async Task<GeoJobResponse> Execute(Guid regionId, RefreshGeoRegionRequest request, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled) throw new GeoException(HttpStatusCode.ServiceUnavailable, "Satellite processing is disabled.");
        var region = await _regions.FindById(regionId, cancellationToken) ?? throw new GeoException(HttpStatusCode.NotFound, "Region not found.");
        /// :: Default windows end at midnight so repeated requests share the same processing identity.
        var now = _time.GetUtcNow().UtcDateTime;
        var end = request.EndAt ?? DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var start = request.StartAt ?? end.AddDays(-_settings.ObservationWindowDays);
        if (start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc || start >= end || end > now || (end - start).TotalDays > _settings.MaximumPeriodDays)
            throw new GeoException(HttpStatusCode.BadRequest, "Provide a valid UTC observation period within the allowed range; end is exclusive.");
        var job = new GeoAnalysisJob(region, start, end, _settings.AlgorithmVersion, JsonSerializer.Serialize(_settings.Parameters()));
        return ToResponse(await _jobs.Enqueue(job, cancellationToken));
    }
    public async Task<GeoJobResponse> GetJob(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await _jobs.FindById(id, cancellationToken) ?? throw new GeoException(HttpStatusCode.NotFound, "Job not found."));
    public static GeoJobResponse ToResponse(GeoAnalysisJob job) => new(job.Id, job.GeoRegionId, job.Status.ToString().ToUpperInvariant(), job.CreatedAt, job.StartedAt, job.FinishedAt, job.ErrorMessage, job.ResultSummary, job.Attempts, job.JobType == UrbanShade.Domain.Enums.GeoJobType.OpenStreetMap ? "OPENSTREETMAP" : "SATELLITE");
}
