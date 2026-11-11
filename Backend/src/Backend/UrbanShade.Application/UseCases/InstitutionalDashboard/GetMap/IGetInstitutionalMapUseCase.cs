using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.InstitutionalDashboard;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetMap;

public interface IGetInstitutionalMapUseCase
{
    Task<InstitutionalMapResponse> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default);
}
