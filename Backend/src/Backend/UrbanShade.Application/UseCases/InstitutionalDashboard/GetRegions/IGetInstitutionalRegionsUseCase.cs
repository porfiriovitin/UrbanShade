using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.InstitutionalDashboard;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetRegions;

public interface IGetInstitutionalRegionsUseCase
{
    Task<PagedResponse<InstitutionalRegionResponse>> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default);
}
