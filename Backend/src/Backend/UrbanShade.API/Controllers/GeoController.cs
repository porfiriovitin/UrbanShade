using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrbanShade.Application.UseCases.GeoRegions.Register;
using UrbanShade.Application.UseCases.GeoRegions.Query;
using UrbanShade.Application.UseCases.GeoRegions.Update;
using UrbanShade.Application.UseCases.GeoAnalysisJobs.Refresh;
using UrbanShade.Application.UseCases.SatelliteAnalyses.Query;
using UrbanShade.Application.UseCases.GreenAreas.Query;
using UrbanShade.Application.UseCases.GreenAreas.Refresh;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Requests.Geo;
using UrbanShade.Communication.Responses;

namespace UrbanShade.API.Controllers;

[ApiController]
[EnableRateLimiting("PublicReads")]
[Authorize]
[Route("api/geo")]
public sealed class GeoController : ControllerBase
{
    [HttpPut("regions/{id:guid}/reference")]
    [Authorize(Policy = "PlatformAdministration")]
    [EnableRateLimiting("GeoAdministration")]
    public async Task<IActionResult> UpdateReference(Guid id, UpdateReferenceRegionRequest request,
        [FromServices] UrbanShade.Application.UseCases.GeoRegions.UpdateReference.IUpdateReferenceRegionUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.Execute(id, request, cancellationToken);
        return NoContent();
    }

    private static PayloadResponse<T> Payload<T>(T data) => new() { Status = nameof(ResponseStatus.Success), Data = data };
    [HttpPost("regions")]
    [Authorize(Policy = "PlatformAdministration")]
    [EnableRateLimiting("GeoAdministration")]
    [RequestSizeLimit(250_000)]
    public async Task<IActionResult> Register(RegisterGeoRegionRequest request, [FromServices] IRegisterGeoRegionUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, Payload(response));
    }
    [HttpGet("regions")]
    public async Task<IActionResult> GetAll([FromServices] IQueryGeoRegionsUseCase useCase, CancellationToken cancellationToken, Guid? cityId = null, int page = 1, int pageSize = 20) =>
        Ok(Payload(await useCase.GetAll(cityId, page, pageSize, cancellationToken)));
    [HttpGet("regions/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromServices] IQueryGeoRegionsUseCase useCase, CancellationToken cancellationToken) =>
        Ok(Payload(await useCase.GetById(id, cancellationToken)));
    [HttpPatch("regions/{id:guid}")]
    [Authorize(Policy = "PlatformAdministration")]
    [EnableRateLimiting("GeoAdministration")]
    [RequestSizeLimit(250_000)]
    public async Task<IActionResult> Update(Guid id, UpdateGeoRegionRequest request, [FromServices] IUpdateGeoRegionUseCase useCase, CancellationToken cancellationToken) =>
        Ok(Payload(await useCase.Execute(id, request, cancellationToken)));
    [HttpDelete("regions/{id:guid}")]
    [Authorize(Policy = "PlatformAdministration")]
    [EnableRateLimiting("GeoAdministration")]
    public async Task<IActionResult> Deactivate(Guid id, [FromServices] IUpdateGeoRegionUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.Deactivate(id, cancellationToken);
        return NoContent();
    }
    [HttpGet("regions/{id:guid}/vegetation")]
    public async Task<IActionResult> Vegetation(Guid id, [FromServices] IQuerySatelliteAnalysesUseCase useCase, CancellationToken cancellationToken) =>
        Ok(Payload(await useCase.Vegetation(id, cancellationToken)));
    [HttpGet("regions/{id:guid}/surface-temperature")]
    public async Task<IActionResult> SurfaceTemperature(Guid id, [FromServices] IQuerySatelliteAnalysesUseCase useCase, CancellationToken cancellationToken) =>
        Ok(Payload(await useCase.Temperature(id, cancellationToken)));
    [HttpGet("regions/{id:guid}/analyses")]
    public async Task<IActionResult> History(Guid id, [FromServices] IQuerySatelliteAnalysesUseCase useCase, CancellationToken cancellationToken, DateTime? startAt = null, DateTime? endAt = null, int page = 1, int pageSize = 20) =>
        Ok(Payload(await useCase.History(id, startAt, endAt, page, pageSize, cancellationToken)));
    [HttpPost("regions/{id:guid}/refresh")]
    [Authorize(Policy = "PlatformAdministration")]
    [EnableRateLimiting("GeoAdministration")]
    public async Task<IActionResult> Refresh(Guid id, [FromServices] IRefreshGeoRegionUseCase useCase, CancellationToken cancellationToken, [FromBody] RefreshGeoRegionRequest? request = null)
    {
        var response = await useCase.Execute(id, request ?? new RefreshGeoRegionRequest(), cancellationToken);
        return AcceptedAtAction(nameof(GetJob), new { id = response.Id }, Payload(response));
    }
    [HttpPost("regions/{id:guid}/refresh-green-areas")]
    [Authorize(Policy = "PlatformAdministration")]
    [EnableRateLimiting("GeoAdministration")]
    public async Task<IActionResult> RefreshGreenAreas(Guid id, [FromServices] IRefreshOpenStreetMapUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(id, cancellationToken);
        return AcceptedAtAction(nameof(GetJob), new { id = response.Id }, Payload(response));
    }
    [HttpGet("jobs/{id:guid}")]
    [Authorize(Policy = "PlatformAdministration")]
    public async Task<IActionResult> GetJob(Guid id, [FromServices] IRefreshGeoRegionUseCase useCase, CancellationToken cancellationToken) =>
        Ok(Payload(await useCase.GetJob(id, cancellationToken)));
    [HttpGet("green-areas")]
    public async Task<IActionResult> GreenAreas([FromQuery] string bbox, [FromServices] IQueryGreenAreasUseCase useCase, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        new JsonResult(await useCase.Execute(bbox, page, pageSize, cancellationToken)) { ContentType = "application/geo+json" };
}
