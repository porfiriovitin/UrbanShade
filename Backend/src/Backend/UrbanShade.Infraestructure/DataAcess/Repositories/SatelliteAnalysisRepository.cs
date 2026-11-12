using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class SatelliteAnalysisRepository : ISatelliteAnalysisRepository
{
    private readonly UrbanShadeDbContext _context;

    public SatelliteAnalysisRepository(UrbanShadeDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsForJob(Guid jobId, GeoAnalysisType type, CancellationToken cancellationToken) => _context.SatelliteAnalyses.AnyAsync(analysis => analysis.GeoAnalysisJobId == jobId && analysis.AnalysisType == type, cancellationToken);

    public void Add(SatelliteAnalysis analysis) => _context.SatelliteAnalyses.Add(analysis);

    public Task<SatelliteAnalysis?> GetLatest(Guid regionId, int boundaryVersion, GeoAnalysisType type, CancellationToken cancellationToken) => _context.SatelliteAnalyses.AsNoTracking().Where(analysis => analysis.GeoRegionId == regionId && analysis.BoundaryVersion == boundaryVersion && analysis.AnalysisType == type).OrderByDescending(analysis => analysis.ObservationEndAt).ThenByDescending(analysis => analysis.ProcessedAt).FirstOrDefaultAsync(cancellationToken);

    public Task<List<SatelliteAnalysis>> GetHistory(Guid regionId, DateTime? start, DateTime? end, int page, int pageSize, CancellationToken cancellationToken) => _context.SatelliteAnalyses.AsNoTracking().Where(analysis => analysis.GeoRegionId == regionId && (!start.HasValue || analysis.ObservationEndAt >= start) && (!end.HasValue || analysis.ObservationStartAt < end)).OrderByDescending(analysis => analysis.ObservationEndAt).ThenBy(analysis => analysis.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
}
