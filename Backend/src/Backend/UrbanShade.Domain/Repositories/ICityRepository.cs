using UrbanShade.Domain.Entities;

namespace UrbanShade.Domain.Repositories;

public interface ICityRepository
{
    Task<City?> FindById(Guid id, CancellationToken cancellationToken);
    Task<List<City>> GetAll(CancellationToken cancellationToken);
    Task<bool> IbgeCodeExists(string ibgeCode, CancellationToken cancellationToken);
    void Add(City city);
}
