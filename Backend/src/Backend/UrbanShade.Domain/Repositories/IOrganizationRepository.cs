using UrbanShade.Domain.Entities;

namespace UrbanShade.Domain.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> FindById(Guid id, CancellationToken cancellationToken);
    Task<Organization?> FindForUpdate(Guid id, CancellationToken cancellationToken);
    Task<bool> DocumentExists(string document, Guid? exceptId, CancellationToken cancellationToken);
    Task<List<Organization>> GetAccessible(Guid userId, bool isAdministrator, CancellationToken cancellationToken);
    Task<List<Guid>> GetActiveIdsForUser(Guid userId, CancellationToken cancellationToken);
    Task<OrganizationMember?> FindMember(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<List<User>> GetMembers(Guid organizationId, CancellationToken cancellationToken);
    Task<List<Sensor>> GetSensors(Guid organizationId, CancellationToken cancellationToken);
    Task<Sensor?> FindSensorForUpdate(Guid sensorId, CancellationToken cancellationToken);
    void Add(Organization organization);
    void Add(OrganizationMember member);
}
