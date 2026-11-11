using UrbanShade.Communication.Requests.SensorReadings;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.SensorReadings;

namespace UrbanShade.Application.UseCases.SensorReadings.GetHistory;

public interface IGetSensorReadingsUseCase
{
    Task<PagedResponse<SensorReadingResponse>> Execute(Guid sensorId, GetSensorReadingsRequest request, bool isAdministrator, CancellationToken cancellationToken = default);
}
