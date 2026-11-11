using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.InstitutionalDashboard;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetRanking;

public interface IGetInstitutionalRankingUseCase
{
    Task<List<InstitutionalRegionResponse>> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default);
}
