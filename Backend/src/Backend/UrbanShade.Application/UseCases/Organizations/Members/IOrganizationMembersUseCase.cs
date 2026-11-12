using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Responses.Authentication;
using UrbanShade.Communication.Responses.Organizations;

namespace UrbanShade.Application.UseCases.Organizations.Members;

public interface IOrganizationMembersUseCase
{
    Task<List<UserResponse>> GetAll(Guid actorId, Guid organizationId, CancellationToken cancellationToken);

    Task<OrganizationMemberResponse> Add(Guid actorId, Guid organizationId, AddOrganizationMemberRequest request, CancellationToken cancellationToken);

    Task Remove(Guid actorId, Guid organizationId, Guid userId, CancellationToken cancellationToken);

    Task<OrganizationMemberResponse> RegeneratePassword(Guid actorId, Guid organizationId, Guid userId, CancellationToken cancellationToken);
}
