using UrbanShade.Communication.Responses.ThermalAnalyses;

namespace UrbanShade.Application.UseCases.ThermalAnalyses.GetOverview;

public interface IGetThermalOverviewUseCase
{
    Task<ThermalOverviewResponse> Execute(CancellationToken cancellationToken = default);

    Task<ThermalOverviewResponse> Execute(Guid? cityId, Guid? organizationId, Guid? actorId, CancellationToken cancellationToken = default);
}
