using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Repositories;

public interface ISensorRepository
{
    Task<Sensor?> FindById(Guid id, CancellationToken cancellationToken);
    Task<bool> CodeExists(string code, Guid? exceptId, CancellationToken cancellationToken);
    Task<List<Sensor>> GetAll(bool activeOnly, SensorStatus? status, string? code, string? name, CancellationToken cancellationToken);
    void Add(Sensor sensor);
}
