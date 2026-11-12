using UrbanShade.Communication.Responses.ThermalAnalyses;

namespace UrbanShade.Application.UseCases.ThermalAnalyses.GetMap;

public interface IGetThermalMapUseCase
{
    Task<ThermalMapResponse> Execute(CancellationToken cancellationToken = default);

    Task<ThermalMapResponse> Execute(Guid? cityId, Guid? organizationId, Guid? actorId, CancellationToken cancellationToken = default);
}
