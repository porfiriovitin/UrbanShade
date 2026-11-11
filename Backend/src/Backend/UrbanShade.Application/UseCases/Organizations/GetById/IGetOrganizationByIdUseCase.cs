using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Responses.Organizations;

namespace UrbanShade.Application.UseCases.Organizations.GetById;

public interface IGetOrganizationByIdUseCase
{
    Task<OrganizationResponse> Execute(Guid actorId, Guid id, CancellationToken cancellationToken = default);
}
