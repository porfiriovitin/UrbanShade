using UrbanShade.Communication.Requests.SensorReadings;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.SensorReadings;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;
using UrbanShade.Application.UseCases.SensorReadings.GetHistory;

namespace UrbanShade.Application.UseCases.SensorReadings.GetLatest;

public sealed class GetLatestSensorReadingUseCase : IGetLatestSensorReadingUseCase
{
    private readonly ISensorRepository _sensors;
    private readonly ISensorReadingRepository _readings;

    public GetLatestSensorReadingUseCase(ISensorRepository sensors, ISensorReadingRepository readings)
    {
        _sensors = sensors;
        _readings = readings;
    }

    public async Task<SensorReadingResponse?> Execute(Guid sensorId, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        var sensor = await _sensors.FindById(sensorId, cancellationToken);
        if (sensor is null || (!isAdministrator && (!sensor.Active || sensor.Status != SensorStatus.ACTIVE)))
            throw new SensorException(ErrorKind.NotFound, "Sensor is unavailable.");

        var reading = await _readings.GetLatest(sensorId, cancellationToken);
        return reading is null ? null : GetSensorReadingsUseCase.ToResponse(reading);
    }
}
