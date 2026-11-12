using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;
using UrbanShade.Application.UseCases.Sensors.GetById;

namespace UrbanShade.Application.UseCases.Sensors.Delete;

public sealed class DeleteSensorUseCase : IDeleteSensorUseCase
{
    private readonly ISensorRepository _sensors;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSensorUseCase(ISensorRepository sensors, IUnitOfWork unitOfWork)
    {
        _sensors = sensors;
        _unitOfWork = unitOfWork;
    }

    public async Task Execute(Guid id, CancellationToken cancellationToken = default)
    {
        var sensor = await _sensors.FindById(id, cancellationToken);
        if (sensor is null)
            throw new SensorException(ErrorKind.NotFound, "Sensor is unavailable.");

        /// :: Preserve the sensor record for historical queries.
        sensor.Delete();
        await _unitOfWork.SaveChanges(cancellationToken);
    }
}
