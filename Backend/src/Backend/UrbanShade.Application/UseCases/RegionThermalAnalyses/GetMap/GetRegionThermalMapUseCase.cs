using System.Net;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;
using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.GetMap;

public sealed class GetRegionThermalMapUseCase : IGetRegionThermalMapUseCase
{
    private readonly IRegionThermalAnalysisRepository _analyses;
    private readonly IGeoRegionRepository _regions;

    public GetRegionThermalMapUseCase(IRegionThermalAnalysisRepository analyses, IGeoRegionRepository regions)
    {
        _analyses = analyses;
        _regions = regions;
    }

    public async Task<RegionThermalMapResponse> Execute(RegionThermalQueryRequest request, CancellationToken cancellationToken = default)
    {
        RegionThermalQueryValidator.ValidateRequest(request);

        var items = await _analyses.GetLatestRegions(null, RegionThermalQueryValidator.ReadBounds(request.Bbox), null, null, true, false, request.Limit, cancellationToken);
        var features = items.Select(item => new RegionThermalMapFeature
        {
            Geometry = GeoJsonGeometry.Write(item.Region.Boundary),
            Properties = RegionThermalAnalysisMapper.ToResponse(item.Analysis, item.Region.Name)
        }).ToList();

        return new RegionThermalMapResponse
        {
            Features = features
        };
    }
}
