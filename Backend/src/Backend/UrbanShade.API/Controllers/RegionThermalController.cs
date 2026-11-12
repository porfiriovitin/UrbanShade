using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.Analyze;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.GetHistory;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.GetRanking;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.Compare;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.GetMap;

namespace UrbanShade.API.Controllers;

[ApiController]
[EnableRateLimiting("PublicReads")]
[Authorize]
[Route("api/thermal/regions")]
public sealed class RegionThermalController : ControllerBase
{
    [HttpPost("{id:guid}/analyze")]
    [Authorize(Policy = "PlatformAdministration")]
    [EnableRateLimiting("RegionThermalAdministration")]
    public async Task<IActionResult> Analyze(Guid id, [FromServices] IAnalyzeRegionThermalUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(id, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<RegionThermalAnalysisResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("{id:guid}/latest")]
    public async Task<IActionResult> GetLatest(Guid id, [FromServices] IGetLatestRegionThermalAnalysisUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(id, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<object>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id, [FromQuery] RegionThermalQueryRequest request, [FromServices] IGetRegionThermalHistoryUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(id, request, User.IsInRole("PlatformAdministrator"), cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<object>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("ranking")]
    public async Task<IActionResult> GetRanking([FromQuery] RegionThermalQueryRequest request, [FromServices] IGetRegionThermalRankingUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<object>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] RegionThermalQueryRequest request, [FromServices] ICompareRegionThermalAnalysesUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<object>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("map")]
    public async Task<IActionResult> GetMap([FromQuery] RegionThermalQueryRequest request, [FromServices] IGetRegionThermalMapUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<object>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }
}

