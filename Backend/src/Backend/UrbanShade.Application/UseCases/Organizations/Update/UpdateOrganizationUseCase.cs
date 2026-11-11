using System.Net;
using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Responses.Organizations;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;
using UrbanShade.Application.UseCases.Organizations.GetById;

namespace UrbanShade.Application.UseCases.Organizations.Update;

public sealed class UpdateOrganizationUseCase : IUpdateOrganizationUseCase
{
    private readonly IOrganizationRepository _organizations;
    private readonly OrganizationAccess _access;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOrganizationUseCase(IOrganizationRepository organizations, OrganizationAccess access, IUnitOfWork unitOfWork)
    {
        _organizations = organizations;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrganizationResponse> Execute(Guid actorId, Guid id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        await _access.GetActor(actorId, true, cancellationToken);
        Validate(request);
        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);
        var organization = await _organizations.FindForUpdate(id, cancellationToken);
        if (organization is null)
            throw new OrganizationException(HttpStatusCode.NotFound, "Organization is unavailable.");
        var document = request.Document.Trim().ToUpperInvariant();
        if (await _organizations.DocumentExists(document, id, cancellationToken))
            throw new OrganizationException(HttpStatusCode.Conflict, "Organization document already registered.");

        organization.Update(request.Name, Enum.Parse<OrganizationType>(request.Type), document, request.ContactEmail, request.ContactPhone, request.Active);
        organization.UpdateTimeZone(request.TimeZoneId);
        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
        return GetOrganizationByIdUseCase.ToResponse(organization);
    }

    private static void Validate(OrganizationRequest request)
    {
        var result = new OrganizationValidator().Validate(request);
        if (!result.IsValid)
            throw new OrganizationException(HttpStatusCode.BadRequest, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }
}
