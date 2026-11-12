using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.SensorReadings;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.Security;

namespace WebApi.Tests.SensorReadings;

public sealed class SensorReadingTests : BaseIntegrationTest
{
    public SensorReadingTests(UrbanShadeApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task History_FiltersDatesPaginatesAndOrdersNewestFirst()
    {
        var sensor = await CreateSensor();
        var date = new DateTime(2026, 11, 11, 13, 0, 0, DateTimeKind.Utc);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            context.SensorReadings.AddRange(Enumerable.Range(0, 5).Select(index => new SensorReading(sensor.Id, 30 + index, 60, date.AddMinutes(index))));
            await context.SaveChangesAsync();
        }

        Authenticate(UserRoles.Citizen);
        var start = Uri.EscapeDataString("2026-11-11T10:01:00-03:00");
        var end = Uri.EscapeDataString("2026-11-11T10:03:00-03:00");
        var response = await Client.GetAsync($"/api/sensors/{sensor.Id}/readings?startDate={start}&endDate={end}&page=1&pageSize=2");
        response.EnsureSuccessStatusCode();
        var first = (await response.Content.ReadFromJsonAsync<PayloadResponse<PagedResponse<SensorReadingResponse>>>())!.Data!;
        Assert.Equal(3, first.TotalItems);
        Assert.Equal(1, first.Page);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(new[] { date.AddMinutes(3), date.AddMinutes(2) }, first.Items.Select(item => item.MeasuredAt));
        Assert.All(first.Items, item => Assert.True(item.IsSimulated));
        Assert.Contains("2026-11-11T13:03:00Z", await response.Content.ReadAsStringAsync());

        var second = await History(sensor.Id, $"?startDate={start}&endDate={end}&page=2&pageSize=2");
        Assert.Equal(date.AddMinutes(1), Assert.Single(second.Items).MeasuredAt);
        Assert.Equal(3, second.TotalItems);
        Assert.Empty((await History(sensor.Id, "?page=10")).Items);

        var latest = (await Client.GetFromJsonAsync<PayloadResponse<SensorReadingResponse>>($"/api/sensors/{sensor.Id}/readings/latest"))!.Data!;
        Assert.Equal(date.AddMinutes(4), latest.MeasuredAt);
        Assert.Equal(34, latest.Temperature);
    }

    [Fact]
    public async Task EqualTimestamps_HaveStablePaginationAndLatest()
    {
        var sensor = await CreateSensor();
        var date = DateTime.UtcNow;
        var readings = Enumerable.Range(0, 3).Select(_ => new SensorReading(sensor.Id, 30, 60, date)).ToList();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            context.SensorReadings.AddRange(readings);
            await context.SaveChangesAsync();
        }
        Authenticate(UserRoles.PlatformAdministrator);
        var first = Assert.Single((await History(sensor.Id, "?page=1&pageSize=1")).Items);
        var second = Assert.Single((await History(sensor.Id, "?page=2&pageSize=1")).Items);
        Assert.NotEqual(first.Id, second.Id);
        var latest = (await Client.GetFromJsonAsync<PayloadResponse<SensorReadingResponse>>($"/api/sensors/{sensor.Id}/readings/latest"))!.Data!;
        Assert.Equal(first.Id, latest.Id);
    }

    [Theory]
    [InlineData(UserRoles.Citizen)]
    [InlineData(UserRoles.Manager)]
    [InlineData(UserRoles.PlatformAdministrator)]
    public async Task Visibility_IsAppliedToHistoryLatestAndMap(UserRoles role)
    {
        var active = await CreateSensor(withReading: true);
        var inactive = await CreateSensor(SensorStatus.INACTIVE, withReading: true);
        var maintenance = await CreateSensor(SensorStatus.MAINTENANCE, withReading: true);
        var deleted = await CreateSensor(deleted: true, withReading: true);
        var empty = await CreateSensor();
        Authenticate(role);

        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/sensors/{active.Id}/readings/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.GetAsync($"/api/sensors/{empty.Id}/readings/latest")).StatusCode);
        Assert.Empty((await History(empty.Id)).Items);
        foreach (var sensor in new[] { inactive, maintenance, deleted })
        {
            var expected = role == UserRoles.PlatformAdministrator ? HttpStatusCode.OK : HttpStatusCode.NotFound;
            Assert.Equal(expected, (await Client.GetAsync($"/api/sensors/{sensor.Id}/readings")).StatusCode);
            var latestExpected = role == UserRoles.PlatformAdministrator && sensor.DeletedAt is null ? HttpStatusCode.OK : HttpStatusCode.NotFound;
            Assert.Equal(latestExpected, (await Client.GetAsync($"/api/sensors/{sensor.Id}/readings/latest")).StatusCode);
        }

        var map = (await Client.GetFromJsonAsync<PayloadResponse<List<LatestSensorReadingResponse>>>("/api/sensors/latest-readings"))!.Data!;
        var point = Assert.Single(map, item => item.SensorId == active.Id);
        Assert.Equal(active.Name, point.SensorName);
        Assert.Equal(active.Latitude, point.Latitude);
        Assert.Equal(active.Longitude, point.Longitude);
        Assert.True(point.IsSimulated);
        Assert.DoesNotContain(map, item => new[] { inactive.Id, maintenance.Id, deleted.Id, empty.Id }.Contains(item.SensorId));
    }

    [Fact]
    public async Task AuthenticationValidationAndMissingSensors_ReturnExpectedErrors()
    {
        var sensor = await CreateSensor();
        foreach (var path in new[] { $"/api/sensors/{sensor.Id}/readings", $"/api/sensors/{sensor.Id}/readings/latest", "/api/sensors/latest-readings" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync(path)).StatusCode);

        Authenticate(UserRoles.PlatformAdministrator);
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?startDate=invalid", "?startDate=2026-11-12T00:00:00Z&endDate=2026-11-11T00:00:00Z" })
        {
            var response = await Client.GetAsync($"/api/sensors/{sensor.Id}/readings{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<PayloadResponse>();
            Assert.Equal("Error", payload!.Status);
            Assert.NotEmpty(payload.Messages);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/sensors/{Guid.NewGuid()}/readings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/sensors/{Guid.NewGuid()}/readings/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await Client.PostAsJsonAsync($"/api/sensors/{sensor.Id}/readings", new { temperature = 30, humidity = 60 })).StatusCode);
    }

    private async Task<Sensor> CreateSensor(SensorStatus status = SensorStatus.ACTIVE, bool deleted = false, bool withReading = false)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var sensor = new Sensor(Guid.NewGuid().ToString("N"), "Reading Test", null, -22.2935, -44.9332, null);
        sensor.UpdateStatus(status);
        if (deleted) sensor.Delete();
        context.Sensors.Add(sensor);
        if (withReading) context.SensorReadings.Add(new SensorReading(sensor.Id, 30.123456m, 60.123456m, DateTime.UtcNow));
        await context.SaveChangesAsync();
        return sensor;
    }

    private void Authenticate(UserRoles role)
    {
        var user = new User("Reading Test", Guid.NewGuid().ToString("N") + "@example.com", "hash");
        user.UpdateRole(role);
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        context.Users.Add(user);
        context.SaveChanges();
        var tokens = new JwtTokenService(new JwtSettings { SigningKey = UrbanShadeApplicationFactory.SigningKey });
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueSession(user));
    }

    private async Task<PagedResponse<SensorReadingResponse>> History(Guid id, string query = "") =>
        (await Client.GetFromJsonAsync<PayloadResponse<PagedResponse<SensorReadingResponse>>>($"/api/sensors/{id}/readings{query}"))!.Data!;
}
