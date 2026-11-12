using UrbanShade.Domain.RegionThermalAnalysis;
using UrbanShade.Domain.ThermalAnalysis;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;
using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;

public sealed class GetLatestRegionThermalAnalysisUseCase : IGetLatestRegionThermalAnalysisUseCase
{
    private readonly IRegionThermalAnalysisRepository _analyses;
    private readonly IGeoRegionRepository _regions;
    private readonly RegionThermalAnalysisSettings _settings;
    private readonly ThermalAnalysisSettings _thermal;
    private readonly TimeProvider _time;

    public GetLatestRegionThermalAnalysisUseCase(IRegionThermalAnalysisRepository analyses, IGeoRegionRepository regions, RegionThermalAnalysisSettings settings, ThermalAnalysisSettings thermal, TimeProvider time)
    {
        _analyses = analyses;
        _regions = regions;
        _settings = settings; _thermal = thermal; _time = time;
    }

    public async Task<RegionThermalAnalysisResponse> Execute(Guid id, CancellationToken cancellationToken = default)
    {
        var region = await RegionThermalQueryValidator.FindRegion(_regions, id, false, cancellationToken);
        var analysis = await _analyses.GetLatest(id, region.BoundaryVersion, cancellationToken)
            ?? throw new RegionThermalAnalysisException(ErrorKind.NotFound, "Analysis not found.");
        var provenance = AnalysisProvenance.Read(analysis.SourceMetadata);
        var validity = AnalysisEligibility.Evaluate(provenance, analysis.AlgorithmVersion, analysis.AnalyzedAt,
            _time.GetUtcNow().UtcDateTime, _settings, _thermal.StaleDataThresholdMinutes);
        if (!analysis.Active || analysis.ExposureIndex is null || analysis.DataQuality == RegionDataQuality.INSUFFICIENT_DATA)
            validity = validity with { IsCurrent = false };
        if (provenance?.Scores.ContainsKey("SurfaceTemperature") == true)
        {
            var reference = region.ReferenceGeoRegionId.HasValue ? await _regions.FindById(region.ReferenceGeoRegionId.Value, cancellationToken) : null;
            if (reference is null || !reference.Active || reference.Id != provenance.ReferenceRegionId || reference.BoundaryVersion != provenance.ReferenceBoundaryVersion)
                validity = validity with { IsCurrent = false };
        }
        return RegionThermalAnalysisMapper.ToResponse(analysis, region.Name, validity);
    }
}

