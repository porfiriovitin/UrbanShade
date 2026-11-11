using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.GetRanking;

public interface IGetRegionThermalRankingUseCase
{
    Task<List<RegionThermalAnalysisResponse>> Execute(RegionThermalQueryRequest request, CancellationToken cancellationToken = default);
}
