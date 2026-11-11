using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.GetHistory;

public interface IGetRegionThermalHistoryUseCase
{
    Task<RegionThermalHistoryResponse> Execute(Guid id, RegionThermalQueryRequest request, bool administrator, CancellationToken cancellationToken = default);
}
