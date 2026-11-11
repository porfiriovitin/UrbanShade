using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class SensorReadingRepository : ISensorReadingRepository
{
    private readonly UrbanShadeDbContext _context;

    public SensorReadingRepository(UrbanShadeDbContext context)
    {
        _context = context;
    }

    public async Task<(List<SensorReading> Items, int TotalItems)> GetHistory(Guid sensorId, DateTime? startDate, DateTime? endDate, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.SensorReadings.AsNoTracking().Where(reading => reading.SensorId == sensorId);

        if (startDate.HasValue) query = query.Where(reading => reading.MeasuredAt >= startDate.Value);
        if (endDate.HasValue) query = query.Where(reading => reading.MeasuredAt <= endDate.Value);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(reading => reading.MeasuredAt).ThenByDescending(reading => reading.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return (items, totalItems);
    }

    public Task<SensorReading?> GetLatest(Guid sensorId, CancellationToken cancellationToken) =>
        _context.SensorReadings.AsNoTracking().Where(reading => reading.SensorId == sensorId)
            .OrderByDescending(reading => reading.MeasuredAt).ThenByDescending(reading => reading.Id).FirstOrDefaultAsync(cancellationToken);

    public Task<List<SensorReading>> GetLatestBySensors(List<Guid> sensorIds, CancellationToken cancellationToken) =>
        _context.SensorReadings.AsNoTracking().Where(reading => sensorIds.Contains(reading.SensorId))
            .GroupBy(reading => reading.SensorId)
            .Select(group => group.OrderByDescending(reading => reading.MeasuredAt).ThenByDescending(reading => reading.Id).First())
            .ToListAsync(cancellationToken);

    public void AddRange(List<SensorReading> readings) => _context.SensorReadings.AddRange(readings);
}
