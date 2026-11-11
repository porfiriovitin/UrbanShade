using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrbanShade.Application.UseCases.Routes.Compare;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Requests.Routes;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Routes;

namespace UrbanShade.API.Controllers;

[ApiController]
[Authorize]
[Route("api/routes")]
public sealed class RoutesController : ControllerBase
{
    [HttpPost("compare")]
    [EnableRateLimiting("Routing")]
    [RequestSizeLimit(4096)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Compare(CompareRoutesRequest request, [FromServices] ICompareRoutesUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<RouteComparisonResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }
}
