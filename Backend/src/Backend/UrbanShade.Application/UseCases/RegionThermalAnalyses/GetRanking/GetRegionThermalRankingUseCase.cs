using System.Net;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;
using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.GetRanking;

public sealed class GetRegionThermalRankingUseCase : IGetRegionThermalRankingUseCase
{
    private readonly IRegionThermalAnalysisRepository _analyses;
    private readonly IGeoRegionRepository _regions;

    public GetRegionThermalRankingUseCase(IRegionThermalAnalysisRepository analyses, IGeoRegionRepository regions)
    {
        _analyses = analyses;
        _regions = regions;
    }

    public async Task<List<RegionThermalAnalysisResponse>> Execute(RegionThermalQueryRequest request, CancellationToken cancellationToken = default)
    {
        RegionThermalQueryValidator.ValidateRequest(request);

        ExposureLevel? level = request.ExposureLevel is null ? null : Enum.Parse<ExposureLevel>(request.ExposureLevel);
        var items = await _analyses.GetLatestRegions(null, null, request.MinExposureIndex, level, request.IncludePartial, true, request.Limit, cancellationToken);

        return items.Select(item => RegionThermalAnalysisMapper.ToResponse(item.Analysis, item.Region.Name)).ToList();
    }
}
