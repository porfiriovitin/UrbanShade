using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;
using UrbanShade.Application.UseCases.Sensors.GetById;

namespace UrbanShade.Application.UseCases.Sensors.Register;

public sealed class RegisterSensorUseCase : IRegisterSensorUseCase
{
    private readonly ISensorRepository _sensors;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICityRepository _cities;

    public RegisterSensorUseCase(ISensorRepository sensors, IUnitOfWork unitOfWork, ICityRepository cities)
    {
        _sensors = sensors;
        _unitOfWork = unitOfWork;
        _cities = cities;
    }

    public async Task<SensorResponse> Execute(RegisterSensorRequest request, CancellationToken cancellationToken = default)
    {
        /// :: Validate and normalize the sensor code.
        Validate(request);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _sensors.CodeExists(code, null, cancellationToken))
            throw new SensorException(ErrorKind.Conflict, "Sensor code already registered.");
        if (request.CityId.HasValue && await _cities.FindById(request.CityId.Value, cancellationToken) is null)
            throw new CityException(ErrorKind.NotFound, "City is unavailable.");

        var sensor = new Sensor(code, request.Name, request.Description, request.Latitude!.Value, request.Longitude!.Value, request.InstalledAt);
        if (request.CityId.HasValue)
            sensor.AssignCity(request.CityId);

        _sensors.Add(sensor);
        await _unitOfWork.SaveChanges(cancellationToken);

        return GetSensorByIdUseCase.ToResponse(sensor);
    }

    private static void Validate(RegisterSensorRequest request)
    {
        var result = new RegisterSensorValidator().Validate(request);
        if (!result.IsValid)
            throw new SensorException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }
}
