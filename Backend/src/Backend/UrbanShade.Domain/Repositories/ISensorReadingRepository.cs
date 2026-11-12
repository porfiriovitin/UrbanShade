using UrbanShade.Domain.Entities;

namespace UrbanShade.Domain.Repositories;

public interface ISensorReadingRepository
{
    Task<(List<SensorReading> Items, int TotalItems)> GetHistory(Guid sensorId, DateTime? startDate, DateTime? endDate, int page, int pageSize, CancellationToken cancellationToken);

    Task<SensorReading?> GetLatest(Guid sensorId, CancellationToken cancellationToken);

    Task<List<SensorReading>> GetLatestBySensors(List<Guid> sensorIds, CancellationToken cancellationToken);

    void AddRange(List<SensorReading> readings);
}
