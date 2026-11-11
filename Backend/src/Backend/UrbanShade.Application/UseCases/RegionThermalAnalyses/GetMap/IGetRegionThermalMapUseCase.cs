using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.GetMap;

public interface IGetRegionThermalMapUseCase
{
    Task<RegionThermalMapResponse> Execute(RegionThermalQueryRequest request, CancellationToken cancellationToken = default);
}
