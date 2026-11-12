using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Repositories;

public interface IThermalAnalysisRepository
{
    Task<List<SensorReading>> GetPending(string algorithmVersion, int take, List<Guid> excludedReadingIds, CancellationToken cancellationToken);

    Task<bool> TryAddForActiveSensor(Entities.ThermalAnalysis analysis, CancellationToken cancellationToken);

    Task<(SensorReading Reading, Entities.ThermalAnalysis Analysis)?> GetLatest(Guid sensorId, string algorithmVersion, CancellationToken cancellationToken);

    Task<List<(SensorReading Reading, Entities.ThermalAnalysis Analysis)>> GetLatestBySensors(List<Guid> sensorIds, string algorithmVersion, CancellationToken cancellationToken);

    Task<(List<(SensorReading Reading, Entities.ThermalAnalysis Analysis)> Items, int TotalItems)> GetHistory(Guid sensorId, string algorithmVersion, DateTime? startDate, DateTime? endDate, ThermalRiskLevel? riskLevel, int page, int pageSize, CancellationToken cancellationToken);
}
