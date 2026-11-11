using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class GeoRegionRepository : IGeoRegionRepository
{
    private readonly UrbanShadeDbContext _context;
    public GeoRegionRepository(UrbanShadeDbContext context) { _context = context; }
    public void Add(GeoRegion region) => _context.GeoRegions.Add(region);
    public Task<GeoRegion?> FindById(Guid id, CancellationToken cancellationToken) => _context.GeoRegions.SingleOrDefaultAsync(region => region.Id == id && region.Active, cancellationToken);
    public Task<List<GeoRegion>> GetAll(Guid? cityId, int page, int pageSize, CancellationToken cancellationToken) =>
        _context.GeoRegions.AsNoTracking().Where(region => region.Active && (!cityId.HasValue || region.CityId == cityId)).OrderBy(region => region.Name).ThenBy(region => region.Id)
        .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
    public async Task<double> GetArea(Geometry boundary, CancellationToken cancellationToken) =>
        await _context.Database.SqlQuery<double>($"SELECT ST_Area(ST_GeomFromText({boundary.AsText()}, 4326)::geography) AS \"Value\"").SingleAsync(cancellationToken);
}
