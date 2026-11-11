using System.Net;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;
using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.Compare;

public sealed class CompareRegionThermalAnalysesUseCase : ICompareRegionThermalAnalysesUseCase
{
    private readonly IRegionThermalAnalysisRepository _analyses;
    private readonly IGeoRegionRepository _regions;

    public CompareRegionThermalAnalysesUseCase(IRegionThermalAnalysisRepository analyses, IGeoRegionRepository regions)
    {
        _analyses = analyses;
        _regions = regions;
    }

    public async Task<RegionThermalComparisonResponse> Execute(RegionThermalQueryRequest request, CancellationToken cancellationToken = default)
    {
        RegionThermalQueryValidator.ValidateRequest(request);
        var ids = RegionThermalQueryValidator.ReadIds(request.Ids)
            ?? throw new RegionThermalAnalysisException(HttpStatusCode.BadRequest, "Provide between 2 and 20 distinct region IDs.");
        var items = await _analyses.GetLatestRegions(ids, null, null, null, true, false, 20, cancellationToken);
        if (items.Count != ids.Count) throw new RegionThermalAnalysisException(HttpStatusCode.NotFound, "An active region or its current analysis was not found.");
        var responses = items.Select(item => RegionThermalAnalysisMapper.ToResponse(item.Analysis, item.Region.Name)).ToList();
        var compositions = responses.Select(item => string.Join(",", item.SourceMetadata.GetProperty("scores").EnumerateObject().Select(property => property.Name).Order())).Distinct().Count();
        return new RegionThermalComparisonResponse { Items = responses, HasComparisonLimitations = compositions > 1 || responses.Any(item => item.DataQuality != "COMPLETE") || responses.Select(item => item.AlgorithmVersion).Distinct().Count() > 1 };
    }
}

