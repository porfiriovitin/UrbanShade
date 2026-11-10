using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;

namespace UrbanShade.Application.UseCases.Sensors.Register;

public interface IRegisterSensorUseCase
{
    Task<SensorResponse> Execute(RegisterSensorRequest request, CancellationToken cancellationToken = default);
}
