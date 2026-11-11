using UrbanShade.Domain.Entities;
namespace UrbanShade.Domain.Repositories;

public interface IGeoAnalysisJobRepository
{
    Task<GeoAnalysisJob> Enqueue(GeoAnalysisJob job, CancellationToken cancellationToken);
    Task<GeoAnalysisJob?> FindById(Guid id, CancellationToken cancellationToken);
    Task<GeoAnalysisJob?> Claim(CancellationToken cancellationToken);
    Task<bool> LockLease(GeoAnalysisJob job, CancellationToken cancellationToken);
    Task<List<GeoRegion>> GetDueRegions(DateTime cutoff, int limit, CancellationToken cancellationToken);
}
