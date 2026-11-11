using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.Compare;

public interface ICompareRegionThermalAnalysesUseCase
{
    Task<RegionThermalComparisonResponse> Execute(RegionThermalQueryRequest request, CancellationToken cancellationToken = default);
}
