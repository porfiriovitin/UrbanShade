using System.Net.Http.Json;
using CommomTestsUtilities.Requests;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Authentication;

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
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", RegisterRequestBuilder.Build());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PayloadResponse<AuthenticationResponse>>())!.Data!;
    }

    protected Task<HttpResponseMessage> Refresh(string token) => Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token });
}
