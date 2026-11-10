using System.Net;
using UrbanShade.Application.UseCases.Authentication.Login.WithEmailAndPassword;
using UrbanShade.Application.UseCases.Authentication.Logout;
using UrbanShade.Application.UseCases.Authentication.Refresh;
using UrbanShade.Application.UseCases.Authentication.Register;
using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Exceptions;

namespace UseCases.Tests.Authentication;

public sealed class RequestValidationTests
{
    [Fact]
    public async Task Register_RejectsInvalidRequestBeforeAccessingDependencies()
    {
        var useCase = new RegisterUseCase(null!, null!, null!, null!);

        var exception = await Assert.ThrowsAsync<AuthenticationException>(() => useCase.Execute(new RegisterRequest()));

        Assert.Equal(HttpStatusCode.BadRequest, exception.GetStatusCode());
        Assert.Equal(new[]
        {
            "Name is required and must have at most 150 characters.",
            "A valid email is required.",
            "Password must contain at least 12 characters."
        }, exception.GetErrorMessages());
    }

    [Fact]
    public async Task Login_RejectsInvalidRequestBeforeAccessingDependencies()
    {
        var useCase = new LoginWithEmailAndPasswordUseCase(null!, null!, null!, null!);

        var exception = await Assert.ThrowsAsync<AuthenticationException>(() => useCase.Execute(new LoginRequest()));

        Assert.Equal(HttpStatusCode.BadRequest, exception.GetStatusCode());
        Assert.Equal(new[]
        {
            "A valid email is required.",
            "Password is required and must contain at most 1024 UTF-8 bytes."
        }, exception.GetErrorMessages());
    }

    [Fact]
    public async Task Refresh_RejectsInvalidRequestBeforeAccessingDependencies()
    {
        var useCase = new RefreshUseCase(null!, null!, null!, null!, null!);

        var exception = await Assert.ThrowsAsync<AuthenticationException>(() => useCase.Execute(new RefreshTokenRequest()));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.GetStatusCode());
        Assert.Equal(new[] { "Invalid refresh token." }, exception.GetErrorMessages());
    }

    [Fact]
    public async Task Logout_RejectsInvalidRequestBeforeAccessingDependencies()
    {
        var useCase = new LogoutUseCase(null!, null!, null!);

        var exception = await Assert.ThrowsAsync<AuthenticationException>(() => useCase.Execute(new RefreshTokenRequest()));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.GetStatusCode());
        Assert.Equal(new[] { "Invalid refresh token." }, exception.GetErrorMessages());
    }
}
