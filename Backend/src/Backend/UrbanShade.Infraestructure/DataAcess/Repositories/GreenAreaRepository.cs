using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class GreenAreaRepository : IGreenAreaRepository
{
    private readonly UrbanShadeDbContext _context;
    public GreenAreaRepository(UrbanShadeDbContext context) { _context = context; }
    public void AddRange(IEnumerable<GreenArea> areas) => _context.GreenAreas.AddRange(areas);
    public Task<List<GreenArea>> GetByBounds(Geometry bounds, int page, int pageSize, CancellationToken cancellationToken) =>
        _context.GreenAreas.AsNoTracking().Where(area => area.Active && area.Geometry.Intersects(bounds) &&
            (area.Source == GreenAreaSource.OpenStreetMap || _context.SatelliteAnalyses.Any(analysis => analysis.Id == area.SatelliteAnalysisId &&
                _context.GeoRegions.Any(region => region.Id == analysis.GeoRegionId && region.Active && region.BoundaryVersion == analysis.BoundaryVersion) &&
                !_context.SatelliteAnalyses.Any(newer => newer.GeoRegionId == analysis.GeoRegionId && newer.BoundaryVersion == analysis.BoundaryVersion && newer.AnalysisType == GeoAnalysisType.Vegetation &&
                    (newer.ObservationEndAt > analysis.ObservationEndAt || (newer.ObservationEndAt == analysis.ObservationEndAt && newer.ProcessedAt > analysis.ProcessedAt))))))
        .OrderBy(area => area.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
}
