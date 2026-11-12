using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;
using UrbanShade.Application.UseCases.Sensors.GetById;

namespace UrbanShade.Application.UseCases.Sensors.Update;

public sealed class UpdateSensorUseCase : IUpdateSensorUseCase
{
    private readonly ISensorRepository _sensors;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICityRepository _cities;

    public UpdateSensorUseCase(ISensorRepository sensors, IUnitOfWork unitOfWork, ICityRepository cities)
    {
        _sensors = sensors;
        _unitOfWork = unitOfWork;
        _cities = cities;
    }

    public async Task<SensorResponse> Execute(Guid id, UpdateSensorRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var sensor = await _sensors.FindById(id, cancellationToken);
        if (sensor is null)
            throw new SensorException(ErrorKind.NotFound, "Sensor is unavailable.");

        var code = request.HasCode ? request.Code!.Trim().ToUpperInvariant() : sensor.Code;
        if (await _sensors.CodeExists(code, id, cancellationToken))
            throw new SensorException(ErrorKind.Conflict, "Sensor code already registered.");

        if (request.HasCityId && request.CityId.HasValue && await _cities.FindById(request.CityId.Value, cancellationToken) is null)
            throw new CityException(ErrorKind.NotFound, "City is unavailable.");

        /// :: Preserve omitted fields and allow clearing optional values.
        if (request.HasCityId) sensor.AssignCity(request.CityId);
        sensor.Update(code, request.HasName ? request.Name! : sensor.Name,
            request.HasDescription ? request.Description : sensor.Description,
            request.HasLatitude ? request.Latitude!.Value : sensor.Latitude,
            request.HasLongitude ? request.Longitude!.Value : sensor.Longitude,
            request.HasInstalledAt ? request.InstalledAt : sensor.InstalledAt);
        await _unitOfWork.SaveChanges(cancellationToken);

        return GetSensorByIdUseCase.ToResponse(sensor);
    }

    private static void Validate(UpdateSensorRequest request)
    {
        var result = new UpdateSensorValidator().Validate(request);
        if (!result.IsValid)
            throw new SensorException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }
}
