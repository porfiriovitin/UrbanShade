using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.InstitutionalDashboard;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetEnvironment;

public interface IGetInstitutionalEnvironmentUseCase
{
    Task<InstitutionalEnvironmentResponse> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default);
}
