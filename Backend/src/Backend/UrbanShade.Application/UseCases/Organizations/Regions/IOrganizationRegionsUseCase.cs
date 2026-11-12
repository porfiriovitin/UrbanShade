using UrbanShade.Communication.Responses.InstitutionalDashboard;

namespace UrbanShade.Application.UseCases.Organizations.Regions;

public interface IOrganizationRegionsUseCase
{
    Task<List<InstitutionalRegionLinkResponse>> GetAll(Guid actorId, Guid organizationId, CancellationToken cancellationToken = default);

    Task Assign(Guid actorId, Guid organizationId, Guid regionId, CancellationToken cancellationToken = default);

    Task Remove(Guid actorId, Guid organizationId, Guid regionId, CancellationToken cancellationToken = default);
}
