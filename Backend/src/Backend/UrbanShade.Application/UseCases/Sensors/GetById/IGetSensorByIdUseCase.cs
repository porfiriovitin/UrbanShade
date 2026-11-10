using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;

namespace UrbanShade.Application.UseCases.Sensors.GetById;

public interface IGetSensorByIdUseCase
{
    Task<SensorResponse> Execute(Guid id, bool isAdministrator, CancellationToken cancellationToken = default);
}
