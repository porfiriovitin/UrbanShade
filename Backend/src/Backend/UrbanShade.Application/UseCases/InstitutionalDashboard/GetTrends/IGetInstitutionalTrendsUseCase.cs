using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.InstitutionalDashboard;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetTrends;

public interface IGetInstitutionalTrendsUseCase
{
    Task<InstitutionalTrendsResponse> Execute(Guid actorId, Guid regionId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default);
}
