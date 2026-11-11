using UrbanShade.Communication.Requests.ThermalAnalyses;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.ThermalAnalyses;

namespace UrbanShade.Application.UseCases.ThermalAnalyses.GetHistory;

public interface IGetThermalAnalysesUseCase
{
    Task<PagedResponse<ThermalAnalysisResponse>> Execute(Guid sensorId, GetThermalAnalysesRequest request, bool isAdministrator, CancellationToken cancellationToken = default);
}
