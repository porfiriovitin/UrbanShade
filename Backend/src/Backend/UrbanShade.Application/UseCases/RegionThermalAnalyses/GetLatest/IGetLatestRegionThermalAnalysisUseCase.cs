using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;

public interface IGetLatestRegionThermalAnalysisUseCase
{
    Task<RegionThermalAnalysisResponse> Execute(Guid id, CancellationToken cancellationToken = default);
}
