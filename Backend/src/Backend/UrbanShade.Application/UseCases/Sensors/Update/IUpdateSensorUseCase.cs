using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;

namespace UrbanShade.Application.UseCases.Sensors.Update;

public interface IUpdateSensorUseCase
{
    Task<SensorResponse> Execute(Guid id, UpdateSensorRequest request, CancellationToken cancellationToken = default);
}
