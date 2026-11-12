using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Authentication;
using UrbanShade.Communication.Responses.Organizations;
using UrbanShade.Communication.Responses.SensorReadings;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Security;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.Security;

namespace WebApi.Tests.Organizations;

public sealed class OrganizationTests : BaseIntegrationTest
{
    private readonly User _admin;
    private const string PermanentPassword = "Permanent-password-123!";
    private const string NextPassword = "Another-password-456!";

    public OrganizationTests(UrbanShadeApplicationFactory factory) : base(factory)
    {
        _admin = new User("Administrator", Email(), "hash");
        _admin.UpdateRole(UserRoles.PlatformAdministrator);
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        context.Users.Add(_admin);
        context.SaveChanges();
        AsAdmin();
    }

    [Fact]
    public async Task Provisioning_RequiresPasswordChangeBeforeNormalAccess_AndReturnsCredentialsOnlyOnce()
    {
        var organization = await CreateOrganization();
        var email = Email();
        var response = await Client.PostAsJsonAsync($"/api/organizations/{organization.Id}/members", new { name = "Manager", email });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var member = (await response.Content.ReadFromJsonAsync<PayloadResponse<OrganizationMemberResponse>>())!.Data!;
        Assert.NotNull(member.TemporaryPassword);
        Assert.NotNull(member.TemporaryPasswordExpiresAt);
        Assert.Equal("Manager", member.User.Role);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var user = await context.Users.SingleAsync(user => user.Id == member.User.Id);
            Assert.True(user.MustChangePassword);
            Assert.NotEqual(member.TemporaryPassword, user.HashedPassword);
            Assert.True(await new Argon2idPasswordHasher().Verify(member.TemporaryPassword!, user.HashedPassword));
            Assert.False(await context.RefreshTokens.AnyAsync(token => token.UserId == user.Id));
        }

        var first = await Login(email, member.TemporaryPassword!);
        Assert.True(first.MustChangePassword);
        Assert.Empty(first.AccessToken);
        Assert.Empty(first.RefreshToken);
        Assert.NotNull(first.FirstAccessToken);
        var jwt = new JsonWebToken(first.FirstAccessToken);
        Assert.Equal("first_access", jwt.GetClaim("purpose").Value);
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddMinutes(10));
        Use(first.FirstAccessToken!);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync("/api/sensors")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync("/api/organizations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync("/api/organizations", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PutAsJsonAsync("/api/auth/password", new { newPassword = PermanentPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/auth/first-access/password", new { newPassword = "short" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/auth/first-access/password", new { newPassword = member.TemporaryPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync("/api/auth/first-access/password", new { newPassword = PermanentPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/first-access/password", new { newPassword = NextPassword })).StatusCode);

        Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/login", new { email, password = member.TemporaryPassword })).StatusCode);
        var normal = await Login(email, PermanentPassword);
        Assert.False(normal.MustChangePassword);
        Assert.NotEmpty(normal.RefreshToken);
        Assert.Contains(organization.Id, normal.OrganizationIds);
        var access = new JsonWebToken(normal.AccessToken);
        Assert.Contains(organization.Id.ToString(), access.Claims.Where(claim => claim.Type == "organization_ids").Select(claim => claim.Value));
        Use(normal.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/organizations/{organization.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync("/api/auth/first-access/password", new { newPassword = NextPassword })).StatusCode);
        var members = await Client.GetAsync($"/api/organizations/{organization.Id}/members");
        Assert.DoesNotContain("temporaryPassword", await members.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        AsAdmin();
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"/api/organizations/{organization.Id}/members/{member.User.Id}/temporary-password", new { })).StatusCode);
    }

    [Fact]
    public async Task ExpiredTemporaryPasswordIsRejected_AndRegenerationInvalidatesOldCredentialsAndTokens()
    {
        var organization = await CreateOrganization();
        var email = Email();
        var member = await AddMember(organization.Id, email);
        var first = await Login(email, member.TemporaryPassword!);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            await context.Users.Where(user => user.Id == member.User.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(user => user.TemporaryPasswordExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/login", new { email, password = member.TemporaryPassword })).StatusCode);

        AsAdmin();
        var regenerated = await Client.PostAsJsonAsync($"/api/organizations/{organization.Id}/members/{member.User.Id}/temporary-password", new { });
        regenerated.EnsureSuccessStatusCode();
        var credentials = (await regenerated.Content.ReadFromJsonAsync<PayloadResponse<OrganizationMemberResponse>>())!.Data!;
        Assert.NotEqual(member.TemporaryPassword, credentials.TemporaryPassword);
        Use(first.FirstAccessToken!);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/first-access/password", new { newPassword = PermanentPassword })).StatusCode);
        Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/login", new { email, password = member.TemporaryPassword })).StatusCode);
        Assert.True((await Login(email, credentials.TemporaryPassword!)).MustChangePassword);
    }

    [Fact]
    public async Task PublicAccountPromotion_ReplacesCredentialsAndSessions_AndCanRepresentMultipleOrganizations()
    {
        var first = await CreateOrganization();
        var second = await CreateOrganization();
        var email = Email();
        var original = await RegisterAndLogin(new() { Name = "Existing", Email = email, Password = PermanentPassword });
        var linked = await AddMember(first.Id, email);
        Assert.NotNull(linked.TemporaryPassword);
        Assert.Equal(original.User.Id, linked.User.Id);
        Assert.Equal("Existing", linked.User.Name);
        Assert.Null((await AddMember(second.Id, email)).TemporaryPassword);

        Use(original.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(original.RefreshToken)).StatusCode);
        Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/login", new { email, password = PermanentPassword })).StatusCode);
        var session = await CompleteAccess(email, linked.TemporaryPassword!);
        Assert.Equal("Manager", session.User.Role);
        Assert.Equal(2, session.OrganizationIds.Count);
        Assert.Contains(first.Id, session.OrganizationIds);
        Assert.Contains(second.Id, session.OrganizationIds);
        Use(session.AccessToken);
        var organizations = (await Client.GetFromJsonAsync<PayloadResponse<List<OrganizationResponse>>>("/api/organizations"))!.Data!;
        Assert.Equal(2, organizations.Count);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync("/api/organizations", Request())).StatusCode);
    }

    [Fact]
    public async Task RevokedMembershipAndInactiveOrganizationOverrideStaleJwtContext()
    {
        var first = await CreateOrganization();
        var second = await CreateOrganization();
        var email = Email();
        var member = await AddMember(first.Id, email);
        await AddMember(second.Id, email);
        var session = await CompleteAccess(email, member.TemporaryPassword!);
        var oldToken = session.AccessToken;
        AsAdmin();
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/organizations/{first.Id}/members/{member.User.Id}")).StatusCode);
        Use(oldToken);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/organizations/{first.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/organizations/{first.Id}/sensors")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/organizations/{first.Id}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/organizations/{second.Id}")).StatusCode);

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", new { session.RefreshToken });
        refreshResponse.EnsureSuccessStatusCode();
        var refreshed = (await refreshResponse.Content.ReadFromJsonAsync<PayloadResponse<AuthenticationResponse>>())!.Data!;
        Assert.DoesNotContain(first.Id, refreshed.OrganizationIds);
        Assert.Contains(second.Id, refreshed.OrganizationIds);

        AsAdmin();
        var request = new UpdateOrganizationRequest { Name = second.Name, Type = second.Type, Document = second.Document, ContactEmail = second.ContactEmail, Active = false };
        (await Client.PutAsJsonAsync($"/api/organizations/{second.Id}", request)).EnsureSuccessStatusCode();
        Use(oldToken);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/organizations/{second.Id}")).StatusCode);
        Assert.Empty((await Client.GetFromJsonAsync<PayloadResponse<List<OrganizationResponse>>>("/api/organizations"))!.Data!);
    }

    [Fact]
    public async Task OrganizationSensorsAreScoped_AndTransfersAreRejected()
    {
        var first = await CreateOrganization();
        var second = await CreateOrganization();
        var email = Email();
        var member = await AddMember(first.Id, email);
        var sensorId = await CreateSensor();
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync($"/api/organizations/{first.Id}/sensors/{sensorId}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync($"/api/organizations/{first.Id}/sensors/{sensorId}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"/api/organizations/{second.Id}/sensors/{sensorId}", new { })).StatusCode);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            context.SensorReadings.Add(new SensorReading(sensorId, 30, 60, DateTime.UtcNow));
            var sensor = await context.Sensors.SingleAsync(sensor => sensor.Id == sensorId);
            sensor.UpdateStatus(SensorStatus.INACTIVE);
            await context.SaveChangesAsync();
        }
        var session = await CompleteAccess(email, member.TemporaryPassword!);
        Use(session.AccessToken);
        var sensors = (await Client.GetFromJsonAsync<PayloadResponse<List<SensorResponse>>>($"/api/organizations/{first.Id}/sensors"))!.Data!;
        Assert.Equal(sensorId, Assert.Single(sensors).Id);
        Assert.Equal(first.Id, sensors[0].OrganizationId);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/organizations/{first.Id}/sensors/{sensorId}/readings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/organizations/{first.Id}/sensors/{sensorId}/readings/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/organizations/{second.Id}/sensors/{sensorId}/readings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/organizations/{first.Id}/sensors/{Guid.NewGuid()}/readings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync($"/api/organizations/{first.Id}/sensors/{sensorId}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/sensors/{sensorId}/readings")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentAssignmentsGiveSensorOnlyOneOwner()
    {
        var first = await CreateOrganization();
        var second = await CreateOrganization();
        var sensorId = await CreateSensor();
        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync($"/api/organizations/{first.Id}/sensors/{sensorId}", new { }),
            Client.PostAsJsonAsync($"/api/organizations/{second.Id}/sensors/{sensorId}", new { }));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ConcurrentFirstAccessIsSingleUse_AndPasswordChangeInvalidatesExistingSessions()
    {
        var organization = await CreateOrganization();
        var email = Email();
        var member = await AddMember(organization.Id, email);
        var first = await Login(email, member.TemporaryPassword!);
        Use(first.FirstAccessToken!);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            Client.PostAsJsonAsync("/api/auth/first-access/password", new { newPassword = PermanentPassword })));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);

        Client.DefaultRequestHeaders.Authorization = null;
        var one = await Login(email, PermanentPassword);
        var two = await Login(email, PermanentPassword);
        Use(one.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PutAsJsonAsync("/api/auth/password", new { currentPassword = "Wrong-password", newPassword = NextPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsJsonAsync("/api/auth/password", new { currentPassword = PermanentPassword, newPassword = NextPassword })).StatusCode);
        foreach (var token in new[] { one.AccessToken, two.AccessToken })
        {
            Use(token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/auth/me")).StatusCode);
        }
        Client.DefaultRequestHeaders.Authorization = null;
        foreach (var refreshToken in new[] { one.RefreshToken, two.RefreshToken })
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/login", new { email, password = PermanentPassword })).StatusCode);
        Assert.NotEmpty((await Login(email, NextPassword)).AccessToken);
    }

    [Fact]
    public async Task DatabaseRoleOverridesAdministrativeToken()
    {
        var request = Request();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            await context.Users.Where(user => user.Id == _admin.Id).ExecuteUpdateAsync(update => update.SetProperty(user => user.Role, UserRoles.Manager));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/organizations", request)).StatusCode);
    }

    [Fact]
    public async Task OrganizationValidationUniquenessAndAnonymousAccess()
    {
        var request = Request();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Client.PostAsJsonAsync("/api/organizations", request)));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/organizations", new { })).StatusCode);
        Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/organizations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/organizations", request)).StatusCode);
    }

    private void AsAdmin() => Use(IssueSession(_admin));
    private void Use(string token) => Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    private static JwtTokenService Tokens() => new(new JwtSettings { SigningKey = UrbanShadeApplicationFactory.SigningKey });
    private static string Email() => Guid.NewGuid().ToString("N") + "@example.com";
    private static OrganizationRequest Request() => new()
    {
        Name = "Test Organization", Type = "Condominium", Document = Guid.NewGuid().ToString("N")[..20],
        ContactEmail = "contact@example.com", ContactPhone = "11999999999"
    };

    private async Task<OrganizationResponse> CreateOrganization()
    {
        AsAdmin();
        var response = await Client.PostAsJsonAsync("/api/organizations", Request());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PayloadResponse<OrganizationResponse>>())!.Data!;
    }

    private async Task<OrganizationMemberResponse> AddMember(Guid organizationId, string email)
    {
        AsAdmin();
        var response = await Client.PostAsJsonAsync($"/api/organizations/{organizationId}/members", new { name = "Manager", email });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PayloadResponse<OrganizationMemberResponse>>())!.Data!;
    }

    private async Task<AuthenticationResponse> Login(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PayloadResponse<AuthenticationResponse>>())!.Data!;
    }

    private async Task<AuthenticationResponse> CompleteAccess(string email, string password)
    {
        Client.DefaultRequestHeaders.Authorization = null;
        var first = await Login(email, password);
        Use(first.FirstAccessToken!);
        (await Client.PostAsJsonAsync("/api/auth/first-access/password", new { newPassword = PermanentPassword })).EnsureSuccessStatusCode();
        Client.DefaultRequestHeaders.Authorization = null;
        return await Login(email, PermanentPassword);
    }

    private async Task<Guid> CreateSensor()
    {
        AsAdmin();
        var response = await Client.PostAsJsonAsync("/api/sensors", new
        {
            code = Guid.NewGuid().ToString("N"), name = "Organization Sensor", latitude = -22, longitude = -44
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PayloadResponse<SensorResponse>>())!.Data!.Id;
    }
}
