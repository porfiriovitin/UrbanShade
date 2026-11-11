using UrbanShade.Communication.Responses.ThermalAnalyses;

namespace UrbanShade.Application.UseCases.ThermalAnalyses.GetLatest;

public interface IGetLatestThermalAnalysisUseCase
{
    Task<ThermalAnalysisResponse> Execute(Guid sensorId, bool isAdministrator, CancellationToken cancellationToken = default);
}
