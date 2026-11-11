using System.Net;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Organizations;

public sealed class OrganizationAccess
{
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;

    public OrganizationAccess(IUserRepository users, IOrganizationRepository organizations)
    {
        _users = users;
        _organizations = organizations;
    }

    public async Task<User> GetActor(Guid userId, bool administratorOnly, CancellationToken cancellationToken)
    {
        /// :: Claims provide context; current database permissions authorize institutional operations.
        var user = await _users.FindById(userId, cancellationToken);
        if (user is null || !user.Active || user.MustChangePassword)
            throw new OrganizationException(HttpStatusCode.Unauthorized, "Account is unavailable.");
        if (administratorOnly && user.Role != UserRoles.PlatformAdministrator)
            throw new OrganizationException(HttpStatusCode.Forbidden, "Platform administration is required.");
        return user;
    }

    public async Task<Organization> Require(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetActor(userId, false, cancellationToken);
        var organization = await _organizations.FindById(organizationId, cancellationToken);
        if (organization is null)
            throw new OrganizationException(HttpStatusCode.NotFound, "Organization is unavailable.");

        if (user.Role != UserRoles.PlatformAdministrator)
        {
            var member = await _organizations.FindMember(organizationId, userId, cancellationToken);
            if (user.Role != UserRoles.Manager || !organization.Active || member is null || !member.Active)
                throw new OrganizationException(HttpStatusCode.NotFound, "Organization is unavailable.");
        }
        return organization;
    }
}
