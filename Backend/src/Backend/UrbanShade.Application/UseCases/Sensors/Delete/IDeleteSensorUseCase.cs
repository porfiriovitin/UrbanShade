using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;

namespace UrbanShade.Application.UseCases.Sensors.Delete;

public interface IDeleteSensorUseCase
{
    Task Execute(Guid id, CancellationToken cancellationToken = default);
}
