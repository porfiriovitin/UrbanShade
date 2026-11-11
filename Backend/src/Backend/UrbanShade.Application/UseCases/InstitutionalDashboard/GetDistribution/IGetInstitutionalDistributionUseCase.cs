using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.InstitutionalDashboard;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetDistribution;

public interface IGetInstitutionalDistributionUseCase
{
    Task<InstitutionalDistributionResponse> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default);
}
