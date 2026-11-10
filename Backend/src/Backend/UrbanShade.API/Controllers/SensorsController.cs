using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanShade.Application.UseCases.Sensors.Register;
using UrbanShade.Application.UseCases.Sensors.GetAll;
using UrbanShade.Application.UseCases.Sensors.GetById;
using UrbanShade.Application.UseCases.Sensors.Update;
using UrbanShade.Application.UseCases.Sensors.UpdateStatus;
using UrbanShade.Application.UseCases.Sensors.Delete;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Domain.Enums;

namespace UrbanShade.API.Controllers;

[ApiController]
[Authorize]
[Route("api/sensors")]
public sealed class SensorsController : ControllerBase
{
    [Authorize(Policy = "PlatformAdministration")]
    [HttpPost]
    public async Task<IActionResult> Register(RegisterSensorRequest request, [FromServices] IRegisterSensorUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = response.Id }, new PayloadResponse<SensorResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetSensorsRequest request, [FromServices] IGetSensorsUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, User.IsInRole(nameof(UserRoles.PlatformAdministrator)), cancellationToken);

        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<List<SensorResponse>>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromServices] IGetSensorByIdUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(id, User.IsInRole(nameof(UserRoles.PlatformAdministrator)), cancellationToken);

        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<SensorResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [Authorize(Policy = "PlatformAdministration")]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateSensorRequest request, [FromServices] IUpdateSensorUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(id, request, cancellationToken);

        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<SensorResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [Authorize(Policy = "PlatformAdministration")]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateSensorStatusRequest request, [FromServices] IUpdateSensorStatusUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(id, request, cancellationToken);

        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<SensorResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [Authorize(Policy = "PlatformAdministration")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] IDeleteSensorUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.Execute(id, cancellationToken);
        return NoContent();
    }
}
