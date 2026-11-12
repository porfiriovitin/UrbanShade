using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.DataAcess.Seeds;
using UrbanShade.Infraestructure.Security;
using UrbanShade.Exceptions;

namespace WebApi.Tests.Sensors;

public sealed class SensorTests : BaseIntegrationTest
{
    public SensorTests(UrbanShadeApplicationFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData(UserRoles.Citizen)]
    [InlineData(UserRoles.Manager)]
    public async Task CommonRoles_CanReadActiveSensorsButCannotManage(UserRoles role)
    {
        Authenticate(role);
        var sensors = await List();
        Assert.NotEmpty(sensors);
        Assert.All(sensors, sensor => Assert.Equal("ACTIVE", sensor.Status));
        var id = sensors[0].Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync("/api/sensors", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PatchAsJsonAsync($"/api/sensors/{id}", new { name = "Changed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PatchAsJsonAsync($"/api/sensors/{id}/status", new { status = "INACTIVE" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.DeleteAsync($"/api/sensors/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/sensors/{id}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_RequiresAuthentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/sensors")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/sensors", Request())).StatusCode);
    }

    [Fact]
    public async Task Administrator_CanManageSensors_AndDeletionPreservesHistory()
    {
        Authenticate(UserRoles.PlatformAdministrator);
        var request = Request();
        var created = await Client.PostAsJsonAsync("/api/sensors", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var sensor = (await created.Content.ReadFromJsonAsync<PayloadResponse<SensorResponse>>())!.Data!;
        Assert.Equal("ACTIVE", sensor.Status);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync(created.Headers.Location)).StatusCode);

        var updated = await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}", new { latitude = -23, description = (string?)null, installedAt = (DateTime?)null });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var data = (await updated.Content.ReadFromJsonAsync<PayloadResponse<SensorResponse>>())!.Data!;
        Assert.Equal(-23, data.Latitude);
        Assert.Equal(sensor.Name, data.Name);
        Assert.Equal(sensor.Longitude, data.Longitude);
        Assert.Null(data.Description);
        Assert.Null(data.InstalledAt);

        foreach (var status in new[] { "MAINTENANCE", "INACTIVE" })
        {
            Authenticate(UserRoles.PlatformAdministrator);
            Assert.Equal(HttpStatusCode.OK, (await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}/status", new { status })).StatusCode);
            Assert.Contains(await List("?status=" + status), value => value.Id == sensor.Id);
            Authenticate(UserRoles.Citizen);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/sensors/{sensor.Id}")).StatusCode);
            Assert.DoesNotContain(await List("?status=" + status), value => value.Id == sensor.Id);
        }

        Authenticate(UserRoles.PlatformAdministrator);
        Assert.Equal(HttpStatusCode.OK, (await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}/status", new { status = "ACTIVE" })).StatusCode);
        Authenticate(UserRoles.Citizen);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/sensors/{sensor.Id}")).StatusCode);
        Authenticate(UserRoles.PlatformAdministrator);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/sensors/{sensor.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/sensors/{sensor.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}", new { name = "Deleted" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}/status", new { status = "ACTIVE" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"/api/sensors/{sensor.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync("/api/sensors", request)).StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var deleted = await context.Sensors.IgnoreQueryFilters().SingleAsync(value => value.Id == sensor.Id);
        Assert.NotNull(deleted.DeletedAt);
        Assert.False(deleted.Active);
    }

    [Fact]
    public async Task DuplicateCode_ReturnsConflictUnderConcurrency()
    {
        Authenticate(UserRoles.PlatformAdministrator);
        var request = Request();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Client.PostAsJsonAsync("/api/sensors", request)));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_RejectsDuplicateCode()
    {
        Authenticate(UserRoles.PlatformAdministrator);
        var first = Request();
        (await Client.PostAsJsonAsync("/api/sensors", first)).EnsureSuccessStatusCode();
        var second = await Client.PostAsJsonAsync("/api/sensors", Request());
        var sensor = (await second.Content.ReadFromJsonAsync<PayloadResponse<SensorResponse>>())!.Data!;
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}", new { code = first.Code })).StatusCode);
    }

    [Fact]
    public async Task InvalidInput_ReturnsBadRequest_AndUnknownSensorReturnsNotFound()
    {
        Authenticate(UserRoles.PlatformAdministrator);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/sensors", new { code = "Invalid", name = "Sensor" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/sensors?status=0")).StatusCode);
        var created = await Client.PostAsJsonAsync("/api/sensors", Request());
        var sensor = (await created.Content.ReadFromJsonAsync<PayloadResponse<SensorResponse>>())!.Data!;
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}", new { latitude = (double?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}/status", new { status = "0" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/sensors/{Guid.NewGuid()}")).StatusCode);
        var filtered = await List("?code=" + sensor.Code.ToLowerInvariant() + "&name=TEST");
        Assert.Single(filtered);
        Assert.Equal(sensor.Id, filtered[0].Id);
    }

    [Fact]
    public async Task Seed_IsIdempotent_AndPreservesChangesAndDeletion()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var sensors = await context.Sensors.Where(sensor => sensor.Code.StartsWith("URB-00")).OrderBy(sensor => sensor.Code).ToListAsync();
        Assert.Equal(5, sensors.Count);
        sensors[0].UpdateStatus(SensorStatus.MAINTENANCE);
        sensors[1].Delete();
        await context.SaveChangesAsync();
        await SensorSeed.Execute(context);
        await SensorSeed.Execute(context);
        context.ChangeTracker.Clear();
        Assert.Equal(5, await context.Sensors.IgnoreQueryFilters().CountAsync(sensor => sensor.Code.StartsWith("URB-00")));
        Assert.Equal(SensorStatus.MAINTENANCE, (await context.Sensors.SingleAsync(sensor => sensor.Code == "URB-001")).Status);
        Assert.False(await context.Sensors.AnyAsync(sensor => sensor.Code == "URB-002"));
        await transaction.RollbackAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Persistence_TranslatesUniqueCodeViolation(bool synchronous)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        context.Sensors.Add(new Sensor("URB-001", "Duplicate", null, 0, 0, null));
        var exception = synchronous
            ? Assert.Throws<SensorException>(() => context.SaveChanges())
            : await Assert.ThrowsAsync<SensorException>(() => context.SaveChangesAsync());
        Assert.Equal(UrbanShade.Exceptions.ErrorKind.Conflict, exception.GetErrorKind());
    }

    private void Authenticate(UserRoles role)
    {
        var user = new User("Sensor Test", Guid.NewGuid().ToString("N") + "@example.com", "hash");
        user.UpdateRole(role);
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        context.Users.Add(user);
        context.SaveChanges();
        var service = new JwtTokenService(new JwtSettings { SigningKey = UrbanShadeApplicationFactory.SigningKey });
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueSession(user));
    }

    [Fact]
    public async Task Listing_PaginatesInSql_AndRejectsOversizedPages()
    {
        Authenticate(UserRoles.Citizen);
        var prefix = "PAGED-" + Guid.NewGuid().ToString("N");
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            context.Sensors.AddRange(Enumerable.Range(0, 103).Select(i => new Sensor(prefix + i.ToString("D3"), prefix, null, 0, 0, null)));
            await context.SaveChangesAsync();
        }
        var first = await List($"?name={prefix}&page=1&pageSize=100");
        var second = await List($"?name={prefix}&page=2&pageSize=100");
        Assert.Equal(100, first.Count);
        Assert.Equal(3, second.Count);
        Assert.Empty(first.Select(s => s.Id).Intersect(second.Select(s => s.Id)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/sensors?pageSize=101")).StatusCode);
    }

    private static RegisterSensorRequest Request() => new()
    {
        Code = "TEST-" + Guid.NewGuid().ToString("N"), Name = "Test Sensor", Description = "Description",
        Latitude = -22.2935, Longitude = -44.9332, InstalledAt = DateTime.UtcNow
    };

    private async Task<List<SensorResponse>> List(string query = "") =>
        (await Client.GetFromJsonAsync<PayloadResponse<List<SensorResponse>>>("/api/sensors" + query))!.Data!;
}
