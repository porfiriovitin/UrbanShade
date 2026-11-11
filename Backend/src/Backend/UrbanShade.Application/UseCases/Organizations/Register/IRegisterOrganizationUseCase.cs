using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Responses.Organizations;

namespace UrbanShade.Application.UseCases.Organizations.Register;

public interface IRegisterOrganizationUseCase
{
    Task<OrganizationResponse> Execute(Guid actorId, OrganizationRequest request, CancellationToken cancellationToken = default);
}
