using System.Text.Json;
using UrbanShade.Application.UseCases.GeoAnalysisJobs.Refresh;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GreenAreas.Refresh;

public sealed class RefreshOpenStreetMapUseCase : IRefreshOpenStreetMapUseCase
{
    private readonly IGeoRegionRepository _regions;
    private readonly IGeoAnalysisJobRepository _jobs;
    private readonly OpenStreetMapSettings _settings;
    private readonly TimeProvider _time;

    public RefreshOpenStreetMapUseCase(IGeoRegionRepository regions, IGeoAnalysisJobRepository jobs, OpenStreetMapSettings settings, TimeProvider time)
    {
        _regions = regions;
        _jobs = jobs;
        _settings = settings;
        _time = time;
    }

    public async Task<GeoJobResponse> Execute(Guid regionId, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
            throw new GeoException(ErrorKind.Unavailable, "OpenStreetMap import is disabled.");

        var region = await _regions.FindById(regionId, cancellationToken) ?? throw new GeoException(ErrorKind.NotFound, "Region not found.");
        if (region.Boundary.EnvelopeInternal.Width > 1 || region.Boundary.EnvelopeInternal.Height > 1)
            throw new GeoException(ErrorKind.Validation, "OpenStreetMap import supports regions within one degree per axis.");

        /// :: Reuse daily imports without satellite credentials.
        var day = _time.GetUtcNow().UtcDateTime.Date;
        var job = new GeoAnalysisJob(region, day, day.AddDays(1), "osm-v1", JsonSerializer.Serialize(_settings.Parameters()), GeoJobType.OpenStreetMap);

        return RefreshGeoRegionUseCase.ToResponse(await _jobs.Enqueue(job, cancellationToken));
    }
}
