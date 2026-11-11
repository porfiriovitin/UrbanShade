using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly UrbanShadeDbContext _context;

    public OrganizationRepository(UrbanShadeDbContext context)
    {
        _context = context;
    }

    public Task<Organization?> FindById(Guid id, CancellationToken cancellationToken) =>
        _context.Organizations.AsNoTracking().SingleOrDefaultAsync(organization => organization.Id == id, cancellationToken);

    public Task<Organization?> FindForUpdate(Guid id, CancellationToken cancellationToken) =>
        _context.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);

    public Task<bool> DocumentExists(string document, Guid? exceptId, CancellationToken cancellationToken) =>
        _context.Organizations.AnyAsync(organization => organization.Document == document && (!exceptId.HasValue || organization.Id != exceptId.Value), cancellationToken);

    public Task<List<Organization>> GetAccessible(Guid userId, bool isAdministrator, CancellationToken cancellationToken) =>
        _context.Organizations.AsNoTracking().Where(organization => isAdministrator ||
            (organization.Active && _context.OrganizationMembers.Any(member => member.OrganizationId == organization.Id && member.UserId == userId && member.Active)))
            .OrderBy(organization => organization.Name).ThenBy(organization => organization.Id).ToListAsync(cancellationToken);

    public Task<List<Guid>> GetActiveIdsForUser(Guid userId, CancellationToken cancellationToken) =>
        _context.OrganizationMembers.Where(member => member.UserId == userId && member.Active &&
            _context.Organizations.Any(organization => organization.Id == member.OrganizationId && organization.Active))
            .OrderBy(member => member.OrganizationId).Select(member => member.OrganizationId).ToListAsync(cancellationToken);

    public Task<OrganizationMember?> FindMember(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        _context.OrganizationMembers.SingleOrDefaultAsync(member => member.OrganizationId == organizationId && member.UserId == userId, cancellationToken);

    public Task<List<User>> GetMembers(Guid organizationId, CancellationToken cancellationToken) =>
        _context.Users.AsNoTracking().Where(user => _context.OrganizationMembers.Any(member => member.OrganizationId == organizationId && member.UserId == user.Id && member.Active))
            .OrderBy(user => user.Name).ToListAsync(cancellationToken);

    public Task<List<Sensor>> GetSensors(Guid organizationId, CancellationToken cancellationToken) =>
        _context.Sensors.AsNoTracking().Where(sensor => sensor.OrganizationId == organizationId).OrderBy(sensor => sensor.Code).ToListAsync(cancellationToken);

    public Task<Sensor?> FindSensorForUpdate(Guid sensorId, CancellationToken cancellationToken) =>
        _context.Sensors.FromSqlInterpolated($"SELECT * FROM \"Sensors\" WHERE \"Id\" = {sensorId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);

    public void Add(Organization organization) => _context.Organizations.Add(organization);
    public void Add(OrganizationMember member) => _context.OrganizationMembers.Add(member);
}
