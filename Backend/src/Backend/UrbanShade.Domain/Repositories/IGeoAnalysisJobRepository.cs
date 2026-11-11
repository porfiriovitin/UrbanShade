using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
namespace UrbanShade.Domain.Repositories;

public interface IGeoAnalysisJobRepository
{
    Task<GeoAnalysisJob> Enqueue(GeoAnalysisJob job, CancellationToken cancellationToken);
    Task<GeoAnalysisJob?> FindById(Guid id, CancellationToken cancellationToken);
    Task<GeoAnalysisJob?> Claim(CancellationToken cancellationToken, GeoJobType jobType = GeoJobType.Satellite);
    Task<bool> LockLease(GeoAnalysisJob job, CancellationToken cancellationToken);
    Task<List<GeoRegion>> GetDueRegions(DateTime cutoff, int limit, CancellationToken cancellationToken, GeoJobType jobType = GeoJobType.Satellite);
}
