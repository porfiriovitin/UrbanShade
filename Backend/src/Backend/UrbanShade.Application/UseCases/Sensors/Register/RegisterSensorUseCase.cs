using System.Net;
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

    public RegisterSensorUseCase(ISensorRepository sensors, IUnitOfWork unitOfWork)
    {
        _sensors = sensors;
        _unitOfWork = unitOfWork;
    }

    public async Task<SensorResponse> Execute(RegisterSensorRequest request, CancellationToken cancellationToken = default)
    {
        /// :: Validate and normalize the sensor code before checking uniqueness.
        Validate(request);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _sensors.CodeExists(code, null, cancellationToken))
            throw new SensorException(HttpStatusCode.Conflict, "Sensor code already registered.");

        var sensor = new Sensor(code, request.Name, request.Description, request.Latitude!.Value, request.Longitude!.Value, request.InstalledAt);
        _sensors.Add(sensor);
        await _unitOfWork.SaveChanges(cancellationToken);

        return GetSensorByIdUseCase.ToResponse(sensor);
    }

    private static void Validate(RegisterSensorRequest request)
    {
        var result = new RegisterSensorValidator().Validate(request);
        if (!result.IsValid)
            throw new SensorException(HttpStatusCode.BadRequest, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }
}
