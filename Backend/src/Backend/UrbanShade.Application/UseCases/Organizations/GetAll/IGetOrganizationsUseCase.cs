using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Responses.Organizations;

namespace UrbanShade.Application.UseCases.Organizations.GetAll;

public interface IGetOrganizationsUseCase
{
    Task<List<OrganizationResponse>> Execute(Guid actorId, CancellationToken cancellationToken = default);
}
