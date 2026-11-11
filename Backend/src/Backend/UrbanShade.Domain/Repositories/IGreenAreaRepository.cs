using NetTopologySuite.Geometries;
using UrbanShade.Domain.Entities;
namespace UrbanShade.Domain.Repositories;

public interface IGreenAreaRepository
{
    void AddRange(IEnumerable<GreenArea> areas);
    Task<List<GreenArea>> GetByBounds(Geometry bounds, int page, int pageSize, CancellationToken cancellationToken);
}
