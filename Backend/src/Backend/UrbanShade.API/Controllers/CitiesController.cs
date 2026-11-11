using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanShade.Application.UseCases.Cities.Register;
using UrbanShade.Application.UseCases.Cities.GetAll;
using UrbanShade.Application.UseCases.Cities.GetById;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Requests.Cities;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Cities;

namespace UrbanShade.API.Controllers;

[ApiController]
[Authorize]
[Route("api/cities")]
public sealed class CitiesController : ControllerBase
{
    [Authorize(Policy = "PlatformAdministration")]
    [HttpPost]
    public async Task<IActionResult> Register(RegisterCityRequest request, [FromServices] IRegisterCityUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, new PayloadResponse<CityResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromServices] IGetCitiesUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<List<CityResponse>>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromServices] IGetCityByIdUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(id, cancellationToken);
        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<CityResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }
}
