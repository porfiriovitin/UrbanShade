using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Responses.Organizations;

namespace UrbanShade.Application.UseCases.Organizations.Update;

public interface IUpdateOrganizationUseCase
{
    Task<OrganizationResponse> Execute(Guid actorId, Guid id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default);
}
