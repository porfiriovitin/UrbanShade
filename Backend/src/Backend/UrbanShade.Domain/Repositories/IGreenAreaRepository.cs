using NetTopologySuite.Geometries;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Geospatial;
namespace UrbanShade.Domain.Repositories;

public interface IGreenAreaRepository
{
    Task<bool> ImportOpenStreetMap(GeoAnalysisJob job, OpenStreetMapSnapshot snapshot, CancellationToken cancellationToken);
    void AddRange(IEnumerable<GreenArea> areas);
    Task<List<GreenArea>> GetByBounds(Geometry bounds, int page, int pageSize, CancellationToken cancellationToken);
}
