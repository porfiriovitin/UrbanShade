using Microsoft.Extensions.Logging;
using System.Text.Json;
using UrbanShade.Application.UseCases.Organizations;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard;

public sealed class InstitutionalDashboardQuery
{
    private readonly OrganizationAccess _access;
    private readonly ILogger<InstitutionalDashboardQuery> _logger;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly InstitutionalDashboardSettings _settings;

    public InstitutionalDashboardQuery(OrganizationAccess access, IInstitutionalDashboardRepository dashboard, InstitutionalDashboardSettings settings, ILogger<InstitutionalDashboardQuery> logger)
    {
        _access = access;
        _dashboard = dashboard;
        _settings = settings;
        _logger = logger;
    }

    public async Task<InstitutionalQuery> Prepare(Guid actorId, InstitutionalDashboardRequest request, Guid? regionId, bool history, CancellationToken cancellationToken)
    {
        Validate(request);
        if (!_settings.Enabled)
            throw new InstitutionalDashboardException(ErrorKind.Conflict, "Institutional dashboard is disabled.");
        if (regionId == Guid.Empty || regionId.HasValue && request.RegionId.HasValue && regionId != request.RegionId)
            throw new InstitutionalDashboardException(ErrorKind.Validation, "Conflicting region IDs.");
        if (history && !request.StartDate.HasValue)
            throw new InstitutionalDashboardException(ErrorKind.Validation, "A history period is required.");

        /// :: Recheck live permissions before any repository cache lookup.
        UrbanShade.Domain.Entities.Organization organization;
        try { organization = await _access.Require(request.OrganizationId, actorId, cancellationToken); }
        catch (OrganizationException exception) when (exception.GetErrorKind() is ErrorKind.NotFound or ErrorKind.Forbidden or ErrorKind.Unauthenticated)
        {
            _logger.LogWarning("Institutional access denied for organization {OrganizationId}", request.OrganizationId);
            throw;
        }
        if (!organization.Active)
            throw new InstitutionalDashboardException(ErrorKind.NotFound, "Organization is unavailable.");
        var query = new InstitutionalQuery
        {
            ActorId = actorId, OrganizationId = organization.Id, TimeZoneId = organization.TimeZoneId,
            RegionId = regionId ?? request.RegionId, RegionIds = request.RegionIds,
            StartDate = request.StartDate, EndDate = request.EndDate, Page = request.Page,
            PageSize = request.PageSize ?? _settings.DefaultPageSize, Limit = request.Limit,
            ExposureLevel = request.ExposureLevel, MinExposureIndex = request.MinExposureIndex,
            Sort = request.Sort, Metric = request.Metric, Interval = request.Interval,
            Bounds = request.Bbox is null ? null : InstitutionalDashboardValidator.ParseBounds(request.Bbox, _settings.MaxMapSpanDegrees),
            IncludeSensors = request.IncludeSensors, IncludeGreenAreas = request.IncludeGreenAreas
        };
        if (query.RegionId.HasValue && query.RegionIds.Length > 0)
            throw new InstitutionalDashboardException(ErrorKind.Validation, "Use regionId or regionIds, not both.");
        if (query.RegionId.HasValue || query.RegionIds.Length > 0)
        {
            var access = await _dashboard.GetRegions(query with { ExposureLevel = null, MinExposureIndex = null, Bounds = null, StartDate = null, EndDate = null, Page = 1, PageSize = 1 }, false, cancellationToken);
            if (access.Total != (query.RegionId.HasValue ? 1 : query.RegionIds.Length))
            {
                _logger.LogWarning("Institutional region access denied for organization {OrganizationId}", query.OrganizationId);
                throw new InstitutionalDashboardException(ErrorKind.NotFound, "Region is unavailable.");
            }
        }
        return query;
    }

    private void Validate(InstitutionalDashboardRequest request)
    {
        var result = new InstitutionalDashboardValidator(_settings).Validate(request);
        if (!result.IsValid)
            throw new InstitutionalDashboardException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }

    public InstitutionalRegionResponse ToResponse(InstitutionalRegionData region, bool includeGeometry = false) => new()
    {
        RegionId = region.RegionId, Name = region.Name, BoundaryVersion = region.BoundaryVersion,
        Geometry = includeGeometry ? JsonSerializer.Deserialize<JsonElement>(region.Geometry) : null,
        SensorCount = region.SensorCount, AverageHeatIndex = region.AverageHeatIndex,
        ExposureIndex = region.ExposureIndex, ExposureLevel = region.IsCurrent ? region.ExposureLevel : "INSUFFICIENT_DATA",
        DataQuality = region.DataQuality, IsStale = region.IsStale,
        VegetationCoveragePercent = region.VegetationCoveragePercent, MeanNdvi = region.MeanNdvi,
        SurfaceTemperature = region.SurfaceTemperature, SurfaceTemperatureAnomaly = region.SurfaceTemperatureAnomaly,
        IsHeatIslandCandidate = region.IsHeatIslandCandidate, DataOrigin = region.DataOrigin,
        AnalysisMode = region.DataOrigin == "SIMULATED_SENSORS" ? "DEMONSTRATION" : "EXPERIMENTAL",
        AlgorithmVersion = region.AlgorithmVersion, AnalyzedAt = region.AnalyzedAt, ValidUntil = region.ValidUntil,
        SourceMetadata = UrbanShade.Application.Mapping.ProvenanceMapper.PublicMetadata(region.SourceMetadata), Priority = UrbanShade.Domain.InstitutionalDashboard.InterventionPriority.Calculate(region.IsCurrent, region.ExposureLevel, region.VegetationCoveragePercent, region.SurfaceTemperatureAnomaly, region.IsHeatIslandCandidate, _settings)
    };



}
