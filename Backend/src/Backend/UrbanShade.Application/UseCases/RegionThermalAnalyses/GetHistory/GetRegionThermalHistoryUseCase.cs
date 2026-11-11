using System.Net;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;
using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.GetHistory;

public sealed class GetRegionThermalHistoryUseCase : IGetRegionThermalHistoryUseCase
{
    private readonly IRegionThermalAnalysisRepository _analyses;
    private readonly IGeoRegionRepository _regions;

    public GetRegionThermalHistoryUseCase(IRegionThermalAnalysisRepository analyses, IGeoRegionRepository regions)
    {
        _analyses = analyses;
        _regions = regions;
    }

    public async Task<RegionThermalHistoryResponse> Execute(Guid id, RegionThermalQueryRequest request, bool administrator, CancellationToken cancellationToken = default)
    {
        RegionThermalQueryValidator.ValidateRequest(request);
        var region = await RegionThermalQueryValidator.FindRegion(_regions, id, administrator, cancellationToken);
        ExposureLevel? level = request.ExposureLevel is null ? null : Enum.Parse<ExposureLevel>(request.ExposureLevel);
        var history = await _analyses.GetHistory(id, request.StartDate, request.EndDate, level, request.Page, request.PageSize, cancellationToken);
        return new RegionThermalHistoryResponse { Items = history.Items.Select(item => RegionThermalAnalysisMapper.ToResponse(item, region.Name)).ToList(), TotalItems = history.Total, Page = request.Page, PageSize = request.PageSize };
    }
}

