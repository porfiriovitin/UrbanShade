using UrbanShade.Communication.Requests.SensorReadings;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.SensorReadings;
using UrbanShade.Communication.Responses.Sensors;

namespace UrbanShade.Application.UseCases.Organizations.Sensors;

public interface IOrganizationSensorsUseCase
{
    Task Assign(Guid actorId, Guid organizationId, Guid sensorId, CancellationToken cancellationToken);

    Task<List<SensorResponse>> GetAll(Guid actorId, Guid organizationId, CancellationToken cancellationToken);

    Task<PagedResponse<SensorReadingResponse>> GetHistory(Guid actorId, Guid organizationId, Guid sensorId, GetSensorReadingsRequest request, CancellationToken cancellationToken);

    Task<SensorReadingResponse?> GetLatest(Guid actorId, Guid organizationId, Guid sensorId, CancellationToken cancellationToken);
}
