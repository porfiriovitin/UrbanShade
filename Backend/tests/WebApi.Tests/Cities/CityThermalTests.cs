using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UrbanShade.Application.UseCases.ThermalAnalyses.ProcessPending;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Cities;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Communication.Responses.ThermalAnalyses;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.ThermalAnalysis;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.Security;

namespace WebApi.Tests.Cities;

public sealed class CityThermalTests : BaseIntegrationTest
{
    public CityThermalTests(UrbanShadeApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task CityRegistration_RequiresAdministration_NormalizesAndRejectsDuplicates()
    {
        var request = new { name = " Goiânia ", state = " go ", ibgeCode = " 5208707 " };
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/cities", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/cities")).StatusCode);
        await Authenticate(UserRoles.Citizen);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync("/api/cities", request)).StatusCode);

        await Authenticate(UserRoles.PlatformAdministrator);
        var created = await Client.PostAsJsonAsync("/api/cities", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var city = (await created.Content.ReadFromJsonAsync<PayloadResponse<CityResponse>>())!.Data!;
        Assert.Equal("Goiânia", city.Name);
        Assert.Equal("GO", city.State);
        Assert.Equal("5208707", city.IbgeCode);
        Assert.EndsWith($"/api/cities/{city.Id}", created.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync("/api/cities", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/cities", new { name = "City", state = "RJ", ibgeCode = "3550308" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/cities/{Guid.NewGuid()}")).StatusCode);

        await Authenticate(UserRoles.Citizen);
        var cities = (await Client.GetFromJsonAsync<PayloadResponse<List<CityResponse>>>("/api/cities"))!.Data!;
        Assert.Contains(cities, item => item.Id == city.Id);
        Assert.Equal(city, (await Client.GetFromJsonAsync<PayloadResponse<CityResponse>>($"/api/cities/{city.Id}"))!.Data!);
    }

    [Fact]
    public async Task ConcurrentCityRegistration_ReturnsOneCreatedAndOneConflict()
    {
        await Authenticate(UserRoles.PlatformAdministrator);
        var request = new { name = "Curitiba", state = "PR", ibgeCode = "4106902" };
        var responses = await Task.WhenAll(Client.PostAsJsonAsync("/api/cities", request), Client.PostAsJsonAsync("/api/cities", request));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SensorRegistrationAndPatch_LinkCity_PreserveOrganization_AndSupportLegacySensors()
    {
        var firstCity = await CreateCity("São Paulo", "SP", "3550308");
        var secondCity = await CreateCity("Rio de Janeiro", "RJ", "3304557");
        var organization = await CreateOrganization();
        await Authenticate(UserRoles.PlatformAdministrator);
        var response = await Client.PostAsJsonAsync("/api/sensors", new
        {
            code = Guid.NewGuid().ToString("N"), name = "City Sensor", latitude = -23, longitude = -46, cityId = firstCity.Id
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sensor = (await response.Content.ReadFromJsonAsync<PayloadResponse<SensorResponse>>())!.Data!;
        Assert.Equal(firstCity.Id, sensor.CityId);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync($"/api/organizations/{organization.Id}/sensors/{sensor.Id}", null)).StatusCode);

        response = await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}", new { cityId = secondCity.Id });
        response.EnsureSuccessStatusCode();
        sensor = (await response.Content.ReadFromJsonAsync<PayloadResponse<SensorResponse>>())!.Data!;
        Assert.Equal(secondCity.Id, sensor.CityId);
        Assert.Equal(organization.Id, sensor.OrganizationId);
        response = await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}", new { name = "Updated" });
        Assert.Equal(secondCity.Id, (await response.Content.ReadFromJsonAsync<PayloadResponse<SensorResponse>>())!.Data!.CityId);

        Assert.Equal(HttpStatusCode.NotFound, (await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}", new { cityId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}", new { cityId = Guid.Empty })).StatusCode);
        response = await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}", new { cityId = (Guid?)null });
        response.EnsureSuccessStatusCode();
        sensor = (await response.Content.ReadFromJsonAsync<PayloadResponse<SensorResponse>>())!.Data!;
        Assert.Null(sensor.CityId);
        Assert.Equal(organization.Id, sensor.OrganizationId);

        response = await Client.PostAsJsonAsync("/api/sensors", new
        {
            code = Guid.NewGuid().ToString("N"), name = "Legacy Sensor", latitude = 0, longitude = 0
        });
        response.EnsureSuccessStatusCode();
        Assert.Null((await response.Content.ReadFromJsonAsync<PayloadResponse<SensorResponse>>())!.Data!.CityId);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.PostAsJsonAsync("/api/sensors", new
        {
            code = Guid.NewGuid().ToString("N"), name = "Invalid City", latitude = 0, longitude = 0, cityId = Guid.NewGuid()
        })).StatusCode);
    }

    [Fact]
    public async Task MunicipalMapAndOverview_CombineOrganizations_AndUseOnlyCurrentDataInCelsius()
    {
        var city = await CreateCity("Belo Horizonte", "MG", "3106200");
        var otherCity = await CreateCity("Vitória", "ES", "3205309");
        var firstOrganization = await CreateOrganization();
        var secondOrganization = await CreateOrganization();
        var first = await CreateSensor(city.Id, firstOrganization.Id, 33.1m, 56.4m);
        var second = await CreateSensor(city.Id, secondOrganization.Id, 35, 60);
        var stale = await CreateSensor(city.Id, firstOrganization.Id, 40, 60, stale: true);
        var empty = await CreateSensor(city.Id, withReading: false);
        var inactive = await CreateSensor(city.Id, status: SensorStatus.INACTIVE);
        var deleted = await CreateSensor(city.Id, deleted: true);
        var outside = await CreateSensor(otherCity.Id);
        var unassigned = await CreateSensor(null);
        await Process();
        await Authenticate(UserRoles.Citizen);

        var map = await Map($"/api/thermal/map?cityId={city.Id}");
        Assert.Equal(4, map.Sensors.Count);
        Assert.All(map.Sensors, sensor => Assert.Equal(city.Id, sensor.CityId));
        Assert.Equal(firstOrganization.Id, Assert.Single(map.Sensors, sensor => sensor.SensorId == first.Id).OrganizationId);
        Assert.Equal(secondOrganization.Id, Assert.Single(map.Sensors, sensor => sensor.SensorId == second.Id).OrganizationId);
        Assert.Equal("STALE", Assert.Single(map.Sensors, sensor => sensor.SensorId == stale.Id).DataStatus);
        Assert.Equal("NO_DATA", Assert.Single(map.Sensors, sensor => sensor.SensorId == empty.Id).DataStatus);
        Assert.DoesNotContain(map.Sensors, sensor => new[] { inactive.Id, deleted.Id, outside.Id, unassigned.Id }.Contains(sensor.SensorId));

        var overview = await Overview($"/api/thermal/overview?cityId={city.Id}");
        Assert.Equal(4, overview.TotalActiveSensors);
        Assert.Equal(2, overview.SensorsWithCurrentData);
        Assert.Equal(34.05m, overview.AverageTemperature);
        Assert.Equal(41.78m, overview.AverageHeatIndex);
        Assert.Equal(45.05m, overview.HighestHeatIndex);
        Assert.Equal(0, overview.RiskDistribution.Low);
        Assert.Equal(0, overview.RiskDistribution.Moderate);
        Assert.Equal(1, overview.RiskDistribution.High);
        Assert.Equal(1, overview.RiskDistribution.Critical);
        Assert.Contains((await Map("/api/thermal/map")).Sensors, sensor => sensor.SensorId == unassigned.Id && sensor.CityId is null);
    }

    [Fact]
    public async Task InstitutionalQueries_IntersectOrganizationAndCity_AndAllowMultipleCities()
    {
        var firstCity = await CreateCity("São Paulo", "SP", "3550308");
        var secondCity = await CreateCity("Rio de Janeiro", "RJ", "3304557");
        var organization = await CreateOrganization();
        var otherOrganization = await CreateOrganization();
        var first = await CreateSensor(firstCity.Id, organization.Id, 33.1m, 56.4m);
        var second = await CreateSensor(secondCity.Id, organization.Id, 35, 60);
        var foreign = await CreateSensor(firstCity.Id, otherOrganization.Id, 40, 60);
        await Process();
        await Authenticate(UserRoles.Manager, organization.Id);

        var map = await Map($"/api/organizations/{organization.Id}/thermal/map");
        Assert.Equal(2, map.Sensors.Count);
        Assert.All(map.Sensors, sensor => Assert.Equal(organization.Id, sensor.OrganizationId));
        Assert.Contains(map.Sensors, sensor => sensor.SensorId == first.Id);
        Assert.Contains(map.Sensors, sensor => sensor.SensorId == second.Id);
        Assert.DoesNotContain(map.Sensors, sensor => sensor.SensorId == foreign.Id);
        var filtered = await Map($"/api/organizations/{organization.Id}/thermal/map?cityId={firstCity.Id}");
        Assert.Equal(first.Id, Assert.Single(filtered.Sensors).SensorId);

        var overview = await Overview($"/api/organizations/{organization.Id}/thermal/overview");
        Assert.Equal(2, overview.TotalActiveSensors);
        Assert.Equal(41.78m, overview.AverageHeatIndex);
        overview = await Overview($"/api/organizations/{organization.Id}/thermal/overview?cityId={firstCity.Id}");
        Assert.Equal(1, overview.TotalActiveSensors);
        Assert.Equal(38.51m, overview.AverageHeatIndex);
        foreach (var operation in new[] { "map", "overview" })
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/organizations/{otherOrganization.Id}/thermal/{operation}?cityId={firstCity.Id}")).StatusCode);

        await Authenticate(UserRoles.PlatformAdministrator);
        Assert.Equal(2, (await Map($"/api/organizations/{organization.Id}/thermal/map")).Sensors.Count);
    }

    [Fact]
    public async Task InstitutionalAuthorization_UsesCurrentMembershipAndOrganizationState()
    {
        var organization = await CreateOrganization();
        var manager = await Authenticate(UserRoles.Manager, organization.Id);
        var path = $"/api/organizations/{organization.Id}/thermal";
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync(path + "/map")).StatusCode);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            (await context.OrganizationMembers.SingleAsync(member => member.OrganizationId == organization.Id && member.UserId == manager.Id)).Deactivate();
            await context.SaveChangesAsync();
        }
        foreach (var operation in new[] { "map", "overview" })
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync(path + "/" + operation)).StatusCode);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            (await context.OrganizationMembers.SingleAsync(member => member.OrganizationId == organization.Id && member.UserId == manager.Id)).Activate();
            var stored = await context.Organizations.SingleAsync(item => item.Id == organization.Id);
            stored.Update(stored.Name, stored.Type, stored.Document, stored.ContactEmail, stored.ContactPhone, false);
            await context.SaveChangesAsync();
        }
        foreach (var operation in new[] { "map", "overview" })
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync(path + "/" + operation)).StatusCode);
        await Authenticate(UserRoles.PlatformAdministrator);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync(path + "/overview")).StatusCode);

        var activeOrganization = await CreateOrganization();
        await Authenticate(UserRoles.Citizen, activeOrganization.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/organizations/{activeOrganization.Id}/thermal/map")).StatusCode);
    }

    [Fact]
    public async Task EmptyAndUnknownCities_ReturnEmptyIndicatorsOrExpectedErrors()
    {
        var city = await CreateCity("Porto Alegre", "RS", "4314902");
        var organization = await CreateOrganization();
        foreach (var path in new[] { "/api/thermal/map", "/api/thermal/overview", $"/api/organizations/{organization.Id}/thermal/map", $"/api/organizations/{organization.Id}/thermal/overview" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync(path)).StatusCode);
        await Authenticate(UserRoles.Manager, organization.Id);
        Assert.Empty((await Map($"/api/thermal/map?cityId={city.Id}")).Sensors);
        var overview = await Overview($"/api/thermal/overview?cityId={city.Id}");
        Assert.Equal(0, overview.TotalActiveSensors);
        Assert.Equal(0, overview.SensorsWithCurrentData);
        Assert.Null(overview.AverageHeatIndex);
        Assert.Null(overview.AverageTemperature);
        Assert.Null(overview.HighestHeatIndex);
        foreach (var prefix in new[] { "/api/thermal", $"/api/organizations/{organization.Id}/thermal" })
        foreach (var operation in new[] { "map", "overview" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"{prefix}/{operation}?cityId={Guid.NewGuid()}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"{prefix}/{operation}?cityId=invalid")).StatusCode);
        }
    }

    [Fact]
    public async Task Database_RejectsUnknownCityForeignKey()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var sensor = new Sensor(Guid.NewGuid().ToString("N"), "Invalid City", null, 0, 0, null);
        sensor.AssignCity(Guid.NewGuid());
        context.Sensors.Add(sensor);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private async Task<City> CreateCity(string name, string state, string code)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var city = await context.Cities.SingleOrDefaultAsync(item => item.IbgeCode == code);
        if (city is not null) return city;
        city = new City(name, state, code);
        context.Cities.Add(city);
        await context.SaveChangesAsync();
        return city;
    }

    private async Task<Organization> CreateOrganization()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var organization = new Organization("Thermal Organization", OrganizationType.Municipality, Guid.NewGuid().ToString("N")[..14], "contact@example.com", null);
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
        return organization;
    }

    private async Task<Sensor> CreateSensor(Guid? cityId, Guid? organizationId = null, decimal temperature = 33.1m, decimal humidity = 56.4m,
        bool stale = false, bool withReading = true, SensorStatus status = SensorStatus.ACTIVE, bool deleted = false)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var sensor = new Sensor(Guid.NewGuid().ToString("N"), "Municipal Sensor", null, -22, -44, null);
        if (cityId.HasValue) sensor.AssignCity(cityId);
        if (organizationId.HasValue) sensor.AssignOrganization(organizationId.Value);
        sensor.UpdateStatus(status);
        if (deleted) sensor.Delete();
        context.Sensors.Add(sensor);
        if (withReading) context.SensorReadings.Add(new SensorReading(sensor.Id, temperature, humidity, DateTime.UtcNow.AddMinutes(stale ? -30 : 0)));
        await context.SaveChangesAsync();
        return sensor;
    }

    private async Task<User> Authenticate(UserRoles role, Guid? organizationId = null)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var user = new User("City Test", Guid.NewGuid().ToString("N") + "@example.com", "hash");
        user.UpdateRole(role);
        context.Users.Add(user);
        if (organizationId.HasValue) context.OrganizationMembers.Add(new OrganizationMember(organizationId.Value, user.Id));
        await context.SaveChangesAsync();
        var tokens = new JwtTokenService(new JwtSettings { SigningKey = UrbanShadeApplicationFactory.SigningKey });
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueSession(user));
        return user;
    }

    private async Task Process()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await new ProcessPendingThermalAnalysesUseCase(scope.ServiceProvider.GetRequiredService<IThermalAnalysisRepository>(), new HeatIndexCalculator(),
            new ThermalAnalysisSettings(), TimeProvider.System, NullLogger<ProcessPendingThermalAnalysesUseCase>.Instance).Execute();
    }

    private async Task<ThermalMapResponse> Map(string path) =>
        (await Client.GetFromJsonAsync<PayloadResponse<ThermalMapResponse>>(path))!.Data!;

    private async Task<ThermalOverviewResponse> Overview(string path) =>
        (await Client.GetFromJsonAsync<PayloadResponse<ThermalOverviewResponse>>(path))!.Data!;
}
