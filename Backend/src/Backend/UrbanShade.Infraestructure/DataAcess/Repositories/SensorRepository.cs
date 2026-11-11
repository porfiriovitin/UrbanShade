using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class SensorRepository : ISensorRepository
{
    private readonly UrbanShadeDbContext _context;

    public SensorRepository(UrbanShadeDbContext context)
    {
        _context = context;
    }

    public Task<Sensor?> FindById(Guid id, CancellationToken cancellationToken) => _context.Sensors.SingleOrDefaultAsync(sensor => sensor.Id == id, cancellationToken);

    public Task<Sensor?> FindForReadingHistory(Guid id, CancellationToken cancellationToken) =>
        _context.Sensors.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(sensor => sensor.Id == id, cancellationToken);

    public Task<List<Sensor>> GetActiveForReadingGeneration(CancellationToken cancellationToken) =>
        _context.Sensors.FromSqlRaw("""
            SELECT * FROM "Sensors"
            WHERE "Status" = 'ACTIVE' AND "Active" = TRUE AND "DeletedAt" IS NULL
            ORDER BY "Id" FOR UPDATE
            """).AsNoTracking().ToListAsync(cancellationToken);

    public Task<bool> CodeExists(string code, Guid? exceptId, CancellationToken cancellationToken) =>
        _context.Sensors.IgnoreQueryFilters().AnyAsync(sensor => sensor.Code == code && (!exceptId.HasValue || sensor.Id != exceptId.Value), cancellationToken);

    public Task<List<Sensor>> GetActiveForThermalMap(Guid? cityId, Guid? organizationId, CancellationToken cancellationToken)
    {
        var query = _context.Sensors.AsNoTracking().Where(sensor => sensor.Active && sensor.Status == SensorStatus.ACTIVE);
        if (cityId.HasValue) query = query.Where(sensor => sensor.CityId == cityId.Value);
        if (organizationId.HasValue) query = query.Where(sensor => sensor.OrganizationId == organizationId.Value);
        return query.OrderBy(sensor => sensor.Code).ToListAsync(cancellationToken);
    }

    public Task<List<Sensor>> GetAll(bool activeOnly, SensorStatus? status, string? code, string? name, CancellationToken cancellationToken)
    {
        var query = _context.Sensors.AsNoTracking();

        /// :: Apply visibility before user filters so inactive sensors remain hidden.
        if (activeOnly) query = query.Where(sensor => sensor.Status == SensorStatus.ACTIVE);
        if (status.HasValue) query = query.Where(sensor => sensor.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var normalized = code.Trim().ToUpperInvariant();
            query = query.Where(sensor => sensor.Code == normalized);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var normalized = name.Trim().ToLowerInvariant();
            query = query.Where(sensor => sensor.Name.ToLower().Contains(normalized));
        }

        return query.OrderBy(sensor => sensor.Code).ToListAsync(cancellationToken);
    }

    public void Add(Sensor sensor) => _context.Sensors.Add(sensor);
}
