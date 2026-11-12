using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Repositories;

public interface ISensorRepository
{
    Task<Sensor?> FindById(Guid id, CancellationToken cancellationToken);

    Task<Sensor?> FindForReadingHistory(Guid id, CancellationToken cancellationToken);

    Task<List<Sensor>> GetActiveForReadingGeneration(CancellationToken cancellationToken);

    Task<List<Sensor>> GetActiveForThermalMap(Guid? cityId, Guid? organizationId, CancellationToken cancellationToken);

    Task<bool> CodeExists(string code, Guid? exceptId, CancellationToken cancellationToken);

    Task<List<Sensor>> GetAll(bool activeOnly, SensorStatus? status, string? code, string? name, CancellationToken cancellationToken, int page = 1, int pageSize = 100);

    void Add(Sensor sensor);
}
