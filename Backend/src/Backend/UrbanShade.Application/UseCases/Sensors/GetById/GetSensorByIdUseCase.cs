using System.Net;
using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Sensors.GetById;

public sealed class GetSensorByIdUseCase : IGetSensorByIdUseCase
{
    private readonly ISensorRepository _sensors;

    public GetSensorByIdUseCase(ISensorRepository sensors)
    {
        _sensors = sensors;
    }

    public async Task<SensorResponse> Execute(Guid id, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        var sensor = await _sensors.FindById(id, cancellationToken);
        if (sensor is null || (!isAdministrator && sensor.Status != SensorStatus.ACTIVE))
            throw new SensorException(HttpStatusCode.NotFound, "Sensor is unavailable.");

        return ToResponse(sensor);
    }

    internal static SensorResponse ToResponse(Sensor sensor) => new()
    {
        Id = sensor.Id,
        OrganizationId = sensor.OrganizationId,
        Code = sensor.Code,
        Name = sensor.Name,
        Description = sensor.Description,
        Latitude = sensor.Latitude,
        Longitude = sensor.Longitude,
        Status = sensor.Status.ToString(),
        InstalledAt = sensor.InstalledAt,
        CreatedAt = sensor.CreatedAt,
        UpdatedAt = sensor.UpdatedAt,
        DeletedAt = sensor.DeletedAt
    };
}
