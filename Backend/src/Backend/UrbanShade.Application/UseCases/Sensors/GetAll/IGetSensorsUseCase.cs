using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;

namespace UrbanShade.Application.UseCases.Sensors.GetAll;

public interface IGetSensorsUseCase
{
    Task<List<SensorResponse>> Execute(GetSensorsRequest request, bool isAdministrator, CancellationToken cancellationToken = default);
}
