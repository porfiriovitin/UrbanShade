using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class CityRepository : ICityRepository
{
    private readonly UrbanShadeDbContext _context;

    public CityRepository(UrbanShadeDbContext context)
    {
        _context = context;
    }

    public Task<City?> FindById(Guid id, CancellationToken cancellationToken) =>
        _context.Cities.AsNoTracking().SingleOrDefaultAsync(city => city.Id == id && city.Active, cancellationToken);

    public Task<List<City>> GetAll(CancellationToken cancellationToken) =>
        _context.Cities.AsNoTracking().Where(city => city.Active).OrderBy(city => city.State).ThenBy(city => city.Name).ThenBy(city => city.Id).ToListAsync(cancellationToken);

    public Task<bool> IbgeCodeExists(string ibgeCode, CancellationToken cancellationToken) =>
        _context.Cities.AnyAsync(city => city.IbgeCode == ibgeCode, cancellationToken);

    public void Add(City city) => _context.Cities.Add(city);
}
