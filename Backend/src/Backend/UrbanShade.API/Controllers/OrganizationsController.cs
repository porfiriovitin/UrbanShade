using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanShade.Application.UseCases.Organizations.Regions;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Application.UseCases.Organizations.Register;
using UrbanShade.Application.UseCases.Organizations.Update;
using UrbanShade.Application.UseCases.Organizations.GetAll;
using UrbanShade.Application.UseCases.Organizations.GetById;
using UrbanShade.Application.UseCases.Organizations.Members;
using UrbanShade.Application.UseCases.Organizations.Sensors;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Requests.SensorReadings;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Authentication;
using UrbanShade.Communication.Responses.Organizations;
using UrbanShade.Communication.Responses.SensorReadings;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Application.UseCases.ThermalAnalyses.GetMap;
using UrbanShade.Application.UseCases.ThermalAnalyses.GetOverview;
using UrbanShade.Communication.Responses.ThermalAnalyses;

namespace UrbanShade.API.Controllers;

[ApiController]
[EnableRateLimiting("PublicReads")]
[Authorize]
[Route("api/organizations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrganizationsController : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet("{id:guid}/thermal/map")]
    public async Task<IActionResult> GetThermalMap(Guid id, [FromQuery] Guid? cityId, [FromServices] IGetThermalMapUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(cityId, id, ActorId, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<ThermalMapResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("{id:guid}/thermal/overview")]
    public async Task<IActionResult> GetThermalOverview(Guid id, [FromQuery] Guid? cityId, [FromServices] IGetThermalOverviewUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(cityId, id, ActorId, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<ThermalOverviewResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("{id:guid}/regions")]
    public async Task<IActionResult> GetRegions(Guid id, [FromServices] IOrganizationRegionsUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.GetAll(ActorId, id, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<List<InstitutionalRegionLinkResponse>>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [Authorize(Policy = "PlatformAdministration")]
    [HttpPost("{id:guid}/regions/{regionId:guid}")]
    public async Task<IActionResult> AssignRegion(Guid id, Guid regionId, [FromServices] IOrganizationRegionsUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.Assign(ActorId, id, regionId, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PlatformAdministration")]
    [HttpDelete("{id:guid}/regions/{regionId:guid}")]
    public async Task<IActionResult> RemoveRegion(Guid id, Guid regionId, [FromServices] IOrganizationRegionsUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.Remove(ActorId, id, regionId, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PlatformAdministration")]
    [HttpPost]
    public async Task<IActionResult> Register(OrganizationRequest request, [FromServices] IRegisterOrganizationUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(ActorId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, new PayloadResponse<OrganizationResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromServices] IGetOrganizationsUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(ActorId, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<List<OrganizationResponse>>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromServices] IGetOrganizationByIdUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(ActorId, id, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<OrganizationResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [Authorize(Policy = "PlatformAdministration")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateOrganizationRequest request, [FromServices] IUpdateOrganizationUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(ActorId, id, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<OrganizationResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id, [FromServices] IOrganizationMembersUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.GetAll(ActorId, id, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<List<UserResponse>>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [Authorize(Policy = "PlatformAdministration")]
    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, AddOrganizationMemberRequest request, [FromServices] IOrganizationMembersUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Add(ActorId, id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PayloadResponse<OrganizationMemberResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [Authorize(Policy = "PlatformAdministration")]
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, [FromServices] IOrganizationMembersUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.Remove(ActorId, id, userId, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PlatformAdministration")]
    [HttpPost("{id:guid}/members/{userId:guid}/temporary-password")]
    public async Task<IActionResult> RegeneratePassword(Guid id, Guid userId, [FromServices] IOrganizationMembersUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.RegeneratePassword(ActorId, id, userId, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<OrganizationMemberResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [Authorize(Policy = "PlatformAdministration")]
    [HttpPost("{id:guid}/sensors/{sensorId:guid}")]
    public async Task<IActionResult> AssignSensor(Guid id, Guid sensorId, [FromServices] IOrganizationSensorsUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.Assign(ActorId, id, sensorId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/sensors")]
    public async Task<IActionResult> GetSensors(Guid id, [FromServices] IOrganizationSensorsUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.GetAll(ActorId, id, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<List<SensorResponse>>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("{id:guid}/sensors/{sensorId:guid}/readings")]
    public async Task<IActionResult> GetReadings(Guid id, Guid sensorId, [FromQuery] GetSensorReadingsRequest request, [FromServices] IOrganizationSensorsUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.GetHistory(ActorId, id, sensorId, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<PagedResponse<SensorReadingResponse>>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("{id:guid}/sensors/{sensorId:guid}/readings/latest")]
    public async Task<IActionResult> GetLatestReading(Guid id, Guid sensorId, [FromServices] IOrganizationSensorsUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.GetLatest(ActorId, id, sensorId, cancellationToken);
        if (response is null) return NoContent();
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<SensorReadingResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }
}
