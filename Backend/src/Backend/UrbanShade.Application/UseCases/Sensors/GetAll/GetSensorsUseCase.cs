using System.Net;
using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;
using UrbanShade.Application.UseCases.Sensors.GetById;

namespace UrbanShade.Application.UseCases.Sensors.GetAll;

public sealed class GetSensorsUseCase : IGetSensorsUseCase
{
    private readonly ISensorRepository _sensors;

    public GetSensorsUseCase(ISensorRepository sensors)
    {
        _sensors = sensors;
    }

    public async Task<List<SensorResponse>> Execute(GetSensorsRequest request, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        Validate(request);
        SensorStatus? status = request.Status is null ? null : Enum.Parse<SensorStatus>(request.Status);
        var sensors = await _sensors.GetAll(!isAdministrator, status, request.Code, request.Name, cancellationToken);

        return [.. sensors.Select(GetSensorByIdUseCase.ToResponse)];
    }

    private static void Validate(GetSensorsRequest request)
    {
        var result = new GetSensorsValidator().Validate(request);
        if (!result.IsValid)
            throw new SensorException(HttpStatusCode.BadRequest, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }
}
