using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using UrbanShade.Application.UseCases.Authentication;
using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Responses.Authentication;
using UrbanShade.Communication.Responses.Organizations;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Security;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Organizations.Members;

public sealed class OrganizationMembersUseCase : IOrganizationMembersUseCase
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUserRepository _users;
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwords;
    private readonly FirstAccessSettings _settings;
    private readonly OrganizationAccess _access;

    public OrganizationMembersUseCase(IOrganizationRepository organizations, IUserRepository users, IAccountRepository accounts, IUnitOfWork unitOfWork, IPasswordHasher passwords, FirstAccessSettings settings, OrganizationAccess access)
    {
        _organizations = organizations;
        _users = users;
        _accounts = accounts;
        _unitOfWork = unitOfWork;
        _passwords = passwords;
        _settings = settings;
        _access = access;
    }

    public async Task<List<UserResponse>> GetAll(Guid actorId, Guid organizationId, CancellationToken cancellationToken)
    {
        await _access.Require(organizationId, actorId, cancellationToken);
        return (await _organizations.GetMembers(organizationId, cancellationToken)).Select(TokenIssuer.ToResponse).ToList();
    }

    public async Task<OrganizationMemberResponse> Add(Guid actorId, Guid organizationId, AddOrganizationMemberRequest request, CancellationToken cancellationToken)
    {
        await _access.GetActor(actorId, true, cancellationToken);
        var result = new OrganizationMemberValidator().Validate(request);
        if (!result.IsValid)
            throw new OrganizationException(HttpStatusCode.BadRequest, [.. result.Errors.Select(error => error.ErrorMessage)]);

        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);
        await RequireActiveOrganization(organizationId, cancellationToken);
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _accounts.FindByEmailForUpdate(email, cancellationToken);
        string? temporaryPassword = null;

        if (user is null)
        {
            temporaryPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
            var hash = await _passwords.Hash(temporaryPassword);
            user = new User(request.Name, email, hash);
            user.UpdateRole(UserRoles.Manager);
            user.SetTemporaryPassword(hash, DateTime.UtcNow.AddHours(_settings.TemporaryPasswordHours));
            _users.Add(user);
        }
        else
        {
            if (!user.Active)
                throw new OrganizationException(HttpStatusCode.Conflict, "The existing account is inactive.");
            // Public registration does not prove identity. Promotion must replace the
            // credentials and sessions established before the administrative handoff.
            if (user.Role == UserRoles.Citizen)
            {
                user.UpdateRole(UserRoles.Manager);
                temporaryPassword = await SetTemporaryPassword(user);
                await _accounts.RevokeSessions(user.Id, DateTime.UtcNow, cancellationToken);
            }
        }

        var member = await _organizations.FindMember(organizationId, user.Id, cancellationToken);
        if (member is not null && member.Active)
            throw new OrganizationException(HttpStatusCode.Conflict, "User is already a member.");
        if (member is null) _organizations.Add(new OrganizationMember(organizationId, user.Id));
        else member.Activate();

        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
        return ToResponse(organizationId, user, temporaryPassword);
    }

    public async Task Remove(Guid actorId, Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await _access.GetActor(actorId, true, cancellationToken);
        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);
        if (await _organizations.FindForUpdate(organizationId, cancellationToken) is null)
            throw new OrganizationException(HttpStatusCode.NotFound, "Organization is unavailable.");
        var member = await _organizations.FindMember(organizationId, userId, cancellationToken);
        if (member is null || !member.Active)
            throw new OrganizationException(HttpStatusCode.NotFound, "Membership is unavailable.");
        member.Deactivate();
        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
    }

    public async Task<OrganizationMemberResponse> RegeneratePassword(Guid actorId, Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await _access.GetActor(actorId, true, cancellationToken);
        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);
        await RequireActiveOrganization(organizationId, cancellationToken);
        var member = await _organizations.FindMember(organizationId, userId, cancellationToken);
        var user = await _accounts.FindForUpdate(userId, cancellationToken);
        if (member is null || !member.Active || user is null || !user.Active)
            throw new OrganizationException(HttpStatusCode.NotFound, "Membership is unavailable.");
        if (!user.MustChangePassword)
            throw new OrganizationException(HttpStatusCode.Conflict, "Only pending first-access credentials can be regenerated.");

        var temporaryPassword = await SetTemporaryPassword(user);
        await _accounts.RevokeSessions(user.Id, DateTime.UtcNow, cancellationToken);
        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
        return ToResponse(organizationId, user, temporaryPassword);
    }

    private async Task RequireActiveOrganization(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await _organizations.FindForUpdate(organizationId, cancellationToken);
        if (organization is null || !organization.Active)
            throw new OrganizationException(HttpStatusCode.NotFound, "Active organization is unavailable.");
    }

    private async Task<string> SetTemporaryPassword(User user)
    {
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        user.SetTemporaryPassword(await _passwords.Hash(password), DateTime.UtcNow.AddHours(_settings.TemporaryPasswordHours));
        return password;
    }

    private static OrganizationMemberResponse ToResponse(Guid organizationId, User user, string? password) => new()
    {
        OrganizationId = organizationId,
        User = TokenIssuer.ToResponse(user),
        TemporaryPassword = password,
        TemporaryPasswordExpiresAt = password is null ? null : user.TemporaryPasswordExpiresAt?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
    };
}
