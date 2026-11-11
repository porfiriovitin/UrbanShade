using UrbanShade.Communication.Requests.SensorReadings;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.SensorReadings;

namespace UrbanShade.Application.UseCases.SensorReadings.GetLatestBySensors;

public interface IGetLatestSensorReadingsUseCase
{
    Task<List<LatestSensorReadingResponse>> Execute(CancellationToken cancellationToken = default);
}
