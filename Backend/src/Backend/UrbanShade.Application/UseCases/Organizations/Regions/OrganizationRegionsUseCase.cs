using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Organizations.Regions;

public sealed class OrganizationRegionsUseCase : IOrganizationRegionsUseCase
{
    private readonly IOrganizationRepository _organizations;
    private readonly IGeoRegionRepository _regions;
    private readonly OrganizationAccess _access;
    private readonly IUnitOfWork _unitOfWork;

    public OrganizationRegionsUseCase(IOrganizationRepository organizations, IGeoRegionRepository regions, OrganizationAccess access, IUnitOfWork unitOfWork)
    {
        _organizations = organizations;
        _regions = regions;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<InstitutionalRegionLinkResponse>> GetAll(Guid actorId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        await _access.Require(organizationId, actorId, cancellationToken);

        var regions = await _organizations.GetRegions(organizationId, cancellationToken);

        return regions.Select(region => new InstitutionalRegionLinkResponse(region.Id, region.Name)).ToList();
    }

    public async Task Assign(Guid actorId, Guid organizationId, Guid regionId, CancellationToken cancellationToken = default)
    {
        await _access.GetActor(actorId, true, cancellationToken);

        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);
        var organization = await _organizations.FindForUpdate(organizationId, cancellationToken);
        var region = await _regions.FindById(regionId, cancellationToken);
        if (organization is null || !organization.Active || region is null || !region.Active)
            throw new OrganizationException(ErrorKind.NotFound, "Organization or region is unavailable.");

        var link = await _organizations.FindRegion(organizationId, regionId, cancellationToken);
        if (link is null)
            _organizations.Add(new OrganizationRegion(organizationId, regionId));
        else
            link.Activate();

        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
    }

    public async Task Remove(Guid actorId, Guid organizationId, Guid regionId, CancellationToken cancellationToken = default)
    {
        await _access.GetActor(actorId, true, cancellationToken);

        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);
        if (await _organizations.FindForUpdate(organizationId, cancellationToken) is null)
            throw new OrganizationException(ErrorKind.NotFound, "Organization is unavailable.");

        var link = await _organizations.FindRegion(organizationId, regionId, cancellationToken);
        if (link is null)
            throw new OrganizationException(ErrorKind.NotFound, "Region association is unavailable.");

        link.Deactivate();
        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
    }
}
