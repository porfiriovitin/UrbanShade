using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanShade.Application.UseCases.Authentication.Login.WithEmailAndPassword;
using UrbanShade.Application.UseCases.Authentication.Register;
using UrbanShade.Application.UseCases.Authentication.Refresh;
using UrbanShade.Application.UseCases.Authentication.Logout;
using UrbanShade.Application.UseCases.Authentication.Me;
using UrbanShade.Application.UseCases.Authentication.ChangePassword;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Authentication;

namespace UrbanShade.API.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthenticationController : ControllerBase
{
    [Authorize(Policy = "FirstAccess")]
    [HttpPost("first-access/password")]
    public async Task<IActionResult> CompleteFirstAccess(ChangePasswordRequest request, [FromServices] IChangePasswordUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var id) || !int.TryParse(User.FindFirst("security_version")?.Value, out var version)) return Unauthorized();
        await useCase.Execute(id, version, request, true, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, [FromServices] IChangePasswordUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var id) || !int.TryParse(User.FindFirst("security_version")?.Value, out var version)) return Unauthorized();
        await useCase.Execute(id, version, request, false, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, [FromServices] IRegisterUseCase useCase, CancellationToken cancellationToken)
    {
        var response = await useCase.Execute(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new PayloadResponse<AuthenticationResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, [FromServices] ILoginWithEmailAndPasswordUseCase useCase, CancellationToken cancellationToken) 
    {
        var response = await useCase.Execute(request, cancellationToken);

        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<AuthenticationResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, [FromServices] IRefreshUseCase useCase, CancellationToken cancellationToken) 
    {
       var response = await useCase.Execute(request, cancellationToken);

        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<AuthenticationResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = response
        });
    }
       

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, [FromServices] ILogoutUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.Execute(request, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me([FromServices] IGetCurrentUserUseCase useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var id)) return Unauthorized();

        return StatusCode(StatusCodes.Status200OK, new PayloadResponse<UserResponse>
        {
            Status = nameof(ResponseStatus.Success),
            Data = await useCase.Execute(id, cancellationToken)
        });
    }

}
