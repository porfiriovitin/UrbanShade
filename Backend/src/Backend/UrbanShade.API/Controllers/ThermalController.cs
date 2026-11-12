using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanShade.Application.UseCases.ThermalAnalyses.GetMap;
using UrbanShade.Application.UseCases.ThermalAnalyses.GetOverview;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.ThermalAnalyses;

namespace UrbanShade.API.Controllers;

[ApiController]
[EnableRateLimiting("PublicReads")]
[Authorize]
[Route("api/thermal")]
public sealed class ThermalController : ControllerBase
{
    [HttpGet("map")]
    public async Task<IActionResult> GetMap([FromQuery] Guid? cityId, [FromServices] IGetThermalMapUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(cityId, null, null, cancellationToken);

        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<ThermalMapResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] Guid? cityId, [FromServices] IGetThermalOverviewUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(cityId, null, null, cancellationToken);

        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<ThermalOverviewResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }
}
