using System.Net;
using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Responses.Organizations;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;
using UrbanShade.Application.UseCases.Organizations.GetById;

namespace UrbanShade.Application.UseCases.Organizations.Register;

public sealed class RegisterOrganizationUseCase : IRegisterOrganizationUseCase
{
    private readonly IOrganizationRepository _organizations;
    private readonly OrganizationAccess _access;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterOrganizationUseCase(IOrganizationRepository organizations, OrganizationAccess access, IUnitOfWork unitOfWork)
    {
        _organizations = organizations;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrganizationResponse> Execute(Guid actorId, OrganizationRequest request, CancellationToken cancellationToken = default)
    {
        await _access.GetActor(actorId, true, cancellationToken);
        Validate(request);
        var document = request.Document.Trim().ToUpperInvariant();
        if (await _organizations.DocumentExists(document, null, cancellationToken))
            throw new OrganizationException(HttpStatusCode.Conflict, "Organization document already registered.");

        var organization = new Organization(request.Name, Enum.Parse<OrganizationType>(request.Type), document, request.ContactEmail, request.ContactPhone);
        _organizations.Add(organization);
        await _unitOfWork.SaveChanges(cancellationToken);
        return GetOrganizationByIdUseCase.ToResponse(organization);
    }

    private static void Validate(OrganizationRequest request)
    {
        var result = new OrganizationValidator().Validate(request);
        if (!result.IsValid)
            throw new OrganizationException(HttpStatusCode.BadRequest, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }
}
