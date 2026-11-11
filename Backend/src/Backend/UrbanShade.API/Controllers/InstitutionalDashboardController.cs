using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Application.UseCases.InstitutionalDashboard.GetOverview;
using UrbanShade.Application.UseCases.InstitutionalDashboard.GetDistribution;
using UrbanShade.Application.UseCases.InstitutionalDashboard.GetRegions;
using UrbanShade.Application.UseCases.InstitutionalDashboard.GetRanking;
using UrbanShade.Application.UseCases.InstitutionalDashboard.GetRegion;
using UrbanShade.Application.UseCases.InstitutionalDashboard.CompareRegions;
using UrbanShade.Application.UseCases.InstitutionalDashboard.GetTrends;
using UrbanShade.Application.UseCases.InstitutionalDashboard.GetSensorsOverview;
using UrbanShade.Application.UseCases.InstitutionalDashboard.GetEnvironment;
using UrbanShade.Application.UseCases.InstitutionalDashboard.GetMap;
using UrbanShade.Application.UseCases.InstitutionalDashboard.ExportReport;

namespace UrbanShade.API.Controllers;

[ApiController]
[Authorize(Policy = "InstitutionalDashboard")]
[EnableRateLimiting("InstitutionalDashboard")]
[Route("api/institutional")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InstitutionalDashboardController : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] InstitutionalDashboardRequest request, [FromServices] IGetInstitutionalOverviewUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        var response = await useCase.Execute(actorId, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<InstitutionalOverviewResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("exposure-distribution")]
    public async Task<IActionResult> GetDistribution([FromQuery] InstitutionalDashboardRequest request, [FromServices] IGetInstitutionalDistributionUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        var response = await useCase.Execute(actorId, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<InstitutionalDistributionResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("regions")]
    public async Task<IActionResult> GetRegions([FromQuery] InstitutionalDashboardRequest request, [FromServices] IGetInstitutionalRegionsUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        var response = await useCase.Execute(actorId, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<PagedResponse<InstitutionalRegionResponse>>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("regions/ranking")]
    public async Task<IActionResult> GetRanking([FromQuery] InstitutionalDashboardRequest request, [FromServices] IGetInstitutionalRankingUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        var response = await useCase.Execute(actorId, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<List<InstitutionalRegionResponse>>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("regions/{id:guid}")]
    public async Task<IActionResult> GetRegion(Guid id, [FromQuery] InstitutionalDashboardRequest request, [FromServices] IGetInstitutionalRegionUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        var response = await useCase.Execute(actorId, id, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<InstitutionalRegionResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("regions/compare")]
    public async Task<IActionResult> CompareRegions([FromQuery] InstitutionalDashboardRequest request, [FromServices] ICompareInstitutionalRegionsUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        var response = await useCase.Execute(actorId, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<List<InstitutionalRegionResponse>>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("regions/{id:guid}/trends")]
    public async Task<IActionResult> GetTrends(Guid id, [FromQuery] InstitutionalDashboardRequest request, [FromServices] IGetInstitutionalTrendsUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        var response = await useCase.Execute(actorId, id, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<InstitutionalTrendsResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("sensors/overview")]
    public async Task<IActionResult> GetSensorsOverview([FromQuery] InstitutionalDashboardRequest request, [FromServices] IGetInstitutionalSensorsOverviewUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        var response = await useCase.Execute(actorId, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<InstitutionalSensorsResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("environment")]
    public async Task<IActionResult> GetEnvironment([FromQuery] InstitutionalDashboardRequest request, [FromServices] IGetInstitutionalEnvironmentUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        var response = await useCase.Execute(actorId, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<InstitutionalEnvironmentResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("map")]
    public async Task<IActionResult> GetMap([FromQuery] InstitutionalDashboardRequest request, [FromServices] IGetInstitutionalMapUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        var response = await useCase.Execute(actorId, request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<InstitutionalMapResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("reports/export")]
    public async Task<IActionResult> ExportReport([FromQuery] InstitutionalDashboardRequest request, [FromServices] IExportInstitutionalReportUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId)) return Unauthorized();
        var response = await useCase.Execute(actorId, request, cancellationToken);
        return File(response.Content, response.ContentType, response.FileName);
    }

}
