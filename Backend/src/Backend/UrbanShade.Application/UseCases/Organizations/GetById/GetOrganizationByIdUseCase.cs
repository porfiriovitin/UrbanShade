using System.Net;
using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Responses.Organizations;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Organizations.GetById;

public sealed class GetOrganizationByIdUseCase : IGetOrganizationByIdUseCase
{
    private readonly IOrganizationRepository _organizations;
    private readonly OrganizationAccess _access;

    public GetOrganizationByIdUseCase(IOrganizationRepository organizations, OrganizationAccess access)
    {
        _organizations = organizations;
        _access = access;
    }

    public async Task<OrganizationResponse> Execute(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await _access.Require(id, actorId, cancellationToken);

        return ToResponse(organization);
    }

    internal static OrganizationResponse ToResponse(Organization organization) => new()
    {
        Id = organization.Id,
        TimeZoneId = organization.TimeZoneId,
        Name = organization.Name,
        Type = organization.Type.ToString(),
        Document = organization.Document,
        ContactEmail = organization.ContactEmail,
        ContactPhone = organization.ContactPhone,
        Active = organization.Active,
        CreatedAt = organization.CreatedAt,
        UpdatedAt = organization.UpdatedAt
    };
}
