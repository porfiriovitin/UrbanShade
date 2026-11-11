using UrbanShade.Communication.Requests.SensorReadings;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.SensorReadings;

namespace UrbanShade.Application.UseCases.SensorReadings.GetLatest;

public interface IGetLatestSensorReadingUseCase
{
    Task<SensorReadingResponse?> Execute(Guid sensorId, bool isAdministrator, CancellationToken cancellationToken = default);
}
