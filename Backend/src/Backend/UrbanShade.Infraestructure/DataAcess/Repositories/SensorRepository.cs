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

    public async Task<List<Sensor>> GetActiveForThermalMap(Guid? cityId, Guid? organizationId, CancellationToken cancellationToken)
    {
        var query = _context.Sensors.AsNoTracking().Where(sensor => sensor.Active && sensor.Status == SensorStatus.ACTIVE);
        if (cityId.HasValue) query = query.Where(sensor => sensor.CityId == cityId.Value);
        if (organizationId.HasValue) query = query.Where(sensor => sensor.OrganizationId == organizationId.Value);
        var sensors = await query.OrderBy(sensor => sensor.Code).ThenBy(sensor => sensor.Id).Take(1001).ToListAsync(cancellationToken);
        if (sensors.Count > 1000)
            throw new UrbanShade.Exceptions.SensorException(System.Net.HttpStatusCode.BadRequest, "Map exceeds 1000 sensors. Select a smaller city or organization scope.");
        return sensors;
    }

    public Task<List<Sensor>> GetAll(bool activeOnly, SensorStatus? status, string? code, string? name, CancellationToken cancellationToken, int page = 1, int pageSize = 100)
    {
        if (page is < 1 or > 10000 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(page));
        var query = _context.Sensors.AsNoTracking();

        /// :: Apply visibility before user filters so inactive sensors remain hidden.
        if (activeOnly) query = query.Where(sensor => sensor.Active && sensor.Status == SensorStatus.ACTIVE);
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

        return query.OrderBy(sensor => sensor.Code).ThenBy(sensor => sensor.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
    }

    public void Add(Sensor sensor) => _context.Sensors.Add(sensor);
}
