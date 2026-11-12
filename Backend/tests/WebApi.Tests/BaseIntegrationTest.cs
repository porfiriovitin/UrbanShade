using System.Net.Http.Json;
using CommomTestsUtilities.Requests;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Authentication;
using UrbanShade.Communication.Requests.Authentication;
using Microsoft.Extensions.DependencyInjection;
using UrbanShade.Domain.Entities;
using UrbanShade.Application.UseCases.Authentication;
using UrbanShade.Infraestructure.DataAcess;

namespace WebApi.Tests;

public abstract class BaseIntegrationTest : IClassFixture<UrbanShadeApplicationFactory>
{
    protected BaseIntegrationTest(UrbanShadeApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
    }

    protected UrbanShadeApplicationFactory Factory { get; }
    protected HttpClient Client { get; }

    protected async Task<AuthenticationResponse> Register()
        => await RegisterAndLogin(RegisterRequestBuilder.Build());

    protected async Task<AuthenticationResponse> RegisterAndLogin(RegisterRequest request)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", request);
        response.EnsureSuccessStatusCode();
        var login = await Client.PostAsJsonAsync("/api/auth/login", new { request.Email, request.Password });
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<PayloadResponse<AuthenticationResponse>>())!.Data!;
    }

    protected string IssueSession(User user)
    {
        using var scope = Factory.Services.CreateScope();
        var response = scope.ServiceProvider.GetRequiredService<TokenIssuer>().Issue(user);
        scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>().SaveChanges();
        return response.AccessToken;
    }

    protected Task<HttpResponseMessage> Refresh(string token) => Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token });
}
