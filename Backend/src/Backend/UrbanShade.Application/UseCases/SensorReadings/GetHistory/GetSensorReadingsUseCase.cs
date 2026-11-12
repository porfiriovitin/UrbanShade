using UrbanShade.Communication.Requests.SensorReadings;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.SensorReadings;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.SensorReadings.GetHistory;

public sealed class GetSensorReadingsUseCase : IGetSensorReadingsUseCase
{
    private readonly ISensorRepository _sensors;
    private readonly ISensorReadingRepository _readings;

    public GetSensorReadingsUseCase(ISensorRepository sensors, ISensorReadingRepository readings)
    {
        _sensors = sensors;
        _readings = readings;
    }

    public async Task<PagedResponse<SensorReadingResponse>> Execute(Guid sensorId, GetSensorReadingsRequest request, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        var result = new GetSensorReadingsValidator().Validate(request);
        if (!result.IsValid)
            throw new SensorException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);

        /// :: Restrict deleted sensors to administrative history.
        var sensor = await _sensors.FindForReadingHistory(sensorId, cancellationToken);
        if (sensor is null || (!isAdministrator && (!sensor.Active || sensor.DeletedAt.HasValue || sensor.Status != SensorStatus.ACTIVE)))
            throw new SensorException(ErrorKind.NotFound, "Sensor is unavailable.");

        var history = await _readings.GetHistory(sensorId, request.StartDate?.UtcDateTime, request.EndDate?.UtcDateTime, request.Page, request.PageSize, cancellationToken);

        return new PagedResponse<SensorReadingResponse>
        {
            Items = history.Items.Select(ToResponse).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = history.TotalItems
        };
    }

    internal static SensorReadingResponse ToResponse(SensorReading reading) => new()
    {
        Id = reading.Id,
        SensorId = reading.SensorId,
        Temperature = reading.Temperature,
        Humidity = reading.Humidity,
        MeasuredAt = reading.MeasuredAt
    };
}
