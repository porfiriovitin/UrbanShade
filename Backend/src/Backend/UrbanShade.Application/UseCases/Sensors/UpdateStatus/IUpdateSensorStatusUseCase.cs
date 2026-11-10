using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;

namespace UrbanShade.Application.UseCases.Sensors.UpdateStatus;

public interface IUpdateSensorStatusUseCase
{
    Task<SensorResponse> Execute(Guid id, UpdateSensorStatusRequest request, CancellationToken cancellationToken = default);
}
