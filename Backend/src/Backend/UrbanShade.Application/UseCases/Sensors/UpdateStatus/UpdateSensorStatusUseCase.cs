using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;
using UrbanShade.Application.UseCases.Sensors.GetById;

namespace UrbanShade.Application.UseCases.Sensors.UpdateStatus;

public sealed class UpdateSensorStatusUseCase : IUpdateSensorStatusUseCase
{
    private readonly ISensorRepository _sensors;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSensorStatusUseCase(ISensorRepository sensors, IUnitOfWork unitOfWork)
    {
        _sensors = sensors;
        _unitOfWork = unitOfWork;
    }

    public async Task<SensorResponse> Execute(Guid id, UpdateSensorStatusRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var sensor = await _sensors.FindById(id, cancellationToken);
        if (sensor is null)
            throw new SensorException(ErrorKind.NotFound, "Sensor is unavailable.");

        sensor.UpdateStatus(Enum.Parse<SensorStatus>(request.Status));
        await _unitOfWork.SaveChanges(cancellationToken);

        return GetSensorByIdUseCase.ToResponse(sensor);
    }

    private static void Validate(UpdateSensorStatusRequest request)
    {
        var result = new UpdateSensorStatusValidator().Validate(request);
        if (!result.IsValid)
            throw new SensorException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }
}
