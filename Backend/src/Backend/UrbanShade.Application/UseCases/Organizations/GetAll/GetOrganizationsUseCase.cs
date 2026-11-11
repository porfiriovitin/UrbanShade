using System.Net;
using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Responses.Organizations;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;
using UrbanShade.Application.UseCases.Organizations.GetById;

namespace UrbanShade.Application.UseCases.Organizations.GetAll;

public sealed class GetOrganizationsUseCase : IGetOrganizationsUseCase
{
    private readonly IOrganizationRepository _organizations;
    private readonly OrganizationAccess _access;

    public GetOrganizationsUseCase(IOrganizationRepository organizations, OrganizationAccess access)
    {
        _organizations = organizations;
        _access = access;
    }

    public async Task<List<OrganizationResponse>> Execute(Guid actorId, CancellationToken cancellationToken = default)
    {
        var actor = await _access.GetActor(actorId, false, cancellationToken);
        if (actor.Role == UserRoles.Citizen) return [];
        var organizations = await _organizations.GetAccessible(actorId, actor.Role == UserRoles.PlatformAdministrator, cancellationToken);
        return organizations.Select(GetOrganizationByIdUseCase.ToResponse).ToList();
    }
}
