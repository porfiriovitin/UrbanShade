using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.InstitutionalDashboard;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetRegion;

public interface IGetInstitutionalRegionUseCase
{
    Task<InstitutionalRegionResponse> Execute(Guid actorId, Guid regionId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default);
}
