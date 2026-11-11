using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrbanShade.Application.UseCases.Weather.Query;
using UrbanShade.Application.UseCases.Weather.Refresh;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Requests.Weather;
using UrbanShade.Communication.Responses;
using UrbanShade.Domain.Enums;

namespace UrbanShade.API.Controllers;

[ApiController]
[Authorize]
[Route("api/weather")]
public sealed class WeatherController : ControllerBase
{
    private static PayloadResponse<T> Payload<T>(T data) => new() { Status = nameof(ResponseStatus.Success), Data = data };
    private ObjectResult NoData<T>(T data) => StatusCode(StatusCodes.Status503ServiceUnavailable,
        new PayloadResponse<T> { Status = nameof(ResponseStatus.Error), Messages = ["No weather data is available for this region."], Data = data });

    [HttpGet("regions/{id:guid}/current")]
    public async Task<IActionResult> Current(Guid id, [FromServices] IQueryWeatherUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Current(id, cancellationToken);
        return response.DataStatus == "NO_DATA" ? NoData(response) : Ok(Payload(response));
    }
    [HttpGet("regions/{id:guid}/forecast")]
    public async Task<IActionResult> Forecast(Guid id, [FromServices] IQueryWeatherUseCase useCase, CancellationToken cancellationToken, int? days = null)
    {
        var response = await useCase.Forecast(id, days, cancellationToken);
        return response.DataStatus == "NO_DATA" ? NoData(response) : Ok(Payload(response));
    }
    [HttpGet("regions/{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, [FromQuery] GetWeatherHistoryRequest request, [FromServices] IQueryWeatherUseCase useCase, CancellationToken cancellationToken) =>
        Ok(Payload(await useCase.History(id, request, User.IsInRole(nameof(UserRoles.PlatformAdministrator)), cancellationToken)));

    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromServices] IQueryWeatherUseCase useCase, CancellationToken cancellationToken) =>
        Ok(Payload(await useCase.Overview(cancellationToken)));

    [HttpPost("regions/{id:guid}/refresh")]
    [Authorize(Policy = "PlatformAdministration")]
    [EnableRateLimiting("WeatherAdministration")]
    public async Task<IActionResult> Refresh(Guid id, [FromServices] IRefreshWeatherUseCase useCase, CancellationToken cancellationToken) =>
        Ok(Payload(await useCase.Execute(id, cancellationToken)));
}
