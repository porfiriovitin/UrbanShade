using System.Net;
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

    public GetLatestRegionThermalAnalysisUseCase(IRegionThermalAnalysisRepository analyses, IGeoRegionRepository regions)
    {
        _analyses = analyses;
        _regions = regions;
    }

    public async Task<RegionThermalAnalysisResponse> Execute(Guid id, CancellationToken cancellationToken = default)
    {
        var region = await RegionThermalQueryValidator.FindRegion(_regions, id, false, cancellationToken);
        var analysis = await _analyses.GetLatest(id, region.BoundaryVersion, cancellationToken)
            ?? throw new RegionThermalAnalysisException(HttpStatusCode.NotFound, "Analysis not found.");
        return RegionThermalAnalysisMapper.ToResponse(analysis, region.Name);
    }
}

