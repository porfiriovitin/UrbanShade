using System.Net;
using UrbanShade.Communication.Requests.SensorReadings;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.SensorReadings;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.SensorReadings.GetLatestBySensors;

public sealed class GetLatestSensorReadingsUseCase : IGetLatestSensorReadingsUseCase
{
    private readonly ISensorRepository _sensors;
    private readonly ISensorReadingRepository _readings;

    public GetLatestSensorReadingsUseCase(ISensorRepository sensors, ISensorReadingRepository readings)
    {
        _sensors = sensors;
        _readings = readings;
    }

    public async Task<List<LatestSensorReadingResponse>> Execute(CancellationToken cancellationToken = default)
    {
        /// :: Show active sensors for every role.
        var sensors = (await _sensors.GetActiveForThermalMap(null, null, cancellationToken)).ToDictionary(sensor => sensor.Id);
        if (sensors.Count == 0) return[];

        var readings = await _readings.GetLatestBySensors(sensors.Keys.ToList(), cancellationToken);

        return readings.Select(reading => new LatestSensorReadingResponse
        {
            Id = reading.Id,
            SensorId = reading.SensorId,
            SensorName = sensors[reading.SensorId].Name,
            Latitude = sensors[reading.SensorId].Latitude,
            Longitude = sensors[reading.SensorId].Longitude,
            Temperature = reading.Temperature,
            Humidity = reading.Humidity,
            MeasuredAt = reading.MeasuredAt
        }).OrderBy(reading => reading.SensorName).ThenBy(reading => reading.SensorId).ToList();
    }
}
