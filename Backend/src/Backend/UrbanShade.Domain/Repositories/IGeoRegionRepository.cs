using NetTopologySuite.Geometries;
using UrbanShade.Domain.Entities;
namespace UrbanShade.Domain.Repositories;

public interface IGeoRegionRepository
{
    void Add(GeoRegion region);
    Task<GeoRegion?> FindById(Guid id, CancellationToken cancellationToken);
    Task<List<GeoRegion>> GetAll(Guid? cityId, int page, int pageSize, CancellationToken cancellationToken);
    Task<double> GetArea(Geometry boundary, CancellationToken cancellationToken);
}
