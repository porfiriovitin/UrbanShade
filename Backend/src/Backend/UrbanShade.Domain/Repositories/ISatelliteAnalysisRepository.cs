using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
namespace UrbanShade.Domain.Repositories;

public interface ISatelliteAnalysisRepository
{
    void Add(SatelliteAnalysis analysis);
    Task<bool> ExistsForJob(Guid jobId, GeoAnalysisType type, CancellationToken cancellationToken);
    Task<SatelliteAnalysis?> GetLatest(Guid regionId, int boundaryVersion, GeoAnalysisType type, CancellationToken cancellationToken);
    Task<List<SatelliteAnalysis>> GetHistory(Guid regionId, DateTime? start, DateTime? end, int page, int pageSize, CancellationToken cancellationToken);
}
