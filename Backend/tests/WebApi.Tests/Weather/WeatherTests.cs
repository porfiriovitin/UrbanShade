using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Weather;
using UrbanShade.Application.UseCases.Weather.Query;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Weather;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.Security;

namespace WebApi.Tests.Weather;

public sealed class WeatherTests : BaseIntegrationTest
{
    private WeatherTestProvider Provider => Factory.Services.GetRequiredService<WeatherTestProvider>();
    public WeatherTests(UrbanShadeApplicationFactory factory) : base(factory) { Provider.Reset(); }

    [Fact]
    public async Task Endpoints_RequireAuthentication_ValidateInputs_AndDescribeMissingData()
    {
        var region = await Region();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync($"/api/weather/regions/{region.Id}/current")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null)).StatusCode);
        await Authenticate(UserRoles.Citizen);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null)).StatusCode);
        var current = await Client.GetAsync($"/api/weather/regions/{region.Id}/current");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, current.StatusCode);
        Assert.Equal("NO_DATA", (await current.Content.ReadFromJsonAsync<PayloadResponse<WeatherCurrentResponse>>())!.Data!.DataStatus);
        var forecast = await Client.GetAsync($"/api/weather/regions/{region.Id}/forecast");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, forecast.StatusCode);
        Assert.Equal("NO_DATA", (await forecast.Content.ReadFromJsonAsync<PayloadResponse<WeatherForecastResponse>>())!.Data!.DataStatus);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/weather/regions/{region.Id}/forecast?days=4")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/weather/regions/{region.Id}/forecast?days=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/weather/regions/{region.Id}/history?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/weather/regions/{region.Id}/history?startDate=2026-11-11T00:00:00Z&endDate=2026-11-10T00:00:00Z")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/weather/regions/{Guid.NewGuid()}/current")).StatusCode);
        var history = await Client.GetFromJsonAsync<PayloadResponse<PagedResponse<WeatherCurrentResponse>>>($"/api/weather/regions/{region.Id}/history");
        Assert.Empty(history!.Data!.Items);
        Assert.Equal(0, Provider.Calls);
    }

    [Fact]
    public async Task Refresh_PersistsRegionPointUtcAndForecasts_UpdatesWithoutDuplicates_AndInvalidatesCache()
    {
        var region = await Region();
        await Authenticate(UserRoles.PlatformAdministrator);
        var refreshed = await Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var first = (await refreshed.Content.ReadFromJsonAsync<PayloadResponse<WeatherCurrentResponse>>())!.Data!;
        Assert.Equal("CURRENT", first.DataStatus);
        Assert.Equal("MODEL_ESTIMATE", first.Attribution.DataNature);
        Assert.Equal("OPEN_METEO", first.Source);
        Assert.Equal(DateTimeKind.Utc, first.FetchedAt!.Value.Kind);
        Assert.True(region.Boundary.Covers(new Point(first.Longitude!.Value, first.Latitude!.Value) { SRID = 4326 }));
        var before = (await Client.GetFromJsonAsync<PayloadResponse<WeatherForecastResponse>>($"/api/weather/regions/{region.Id}/forecast"))!.Data!;
        Assert.Equal(72, before.Hourly.Count);
        Assert.All(before.Hourly, item => { Assert.True(item.ForecastAt > DateTime.UtcNow); Assert.Equal(8, item.UvIndex); Assert.Equal(780, item.SolarRadiation); });
        Assert.Equal(24, (await Client.GetFromJsonAsync<PayloadResponse<WeatherForecastResponse>>($"/api/weather/regions/{region.Id}/forecast?days=1"))!.Data!.Hourly.Count);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null)).StatusCode);
        await AllowRefresh(region.Id);
        Provider.Temperature = 35;
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null)).StatusCode);
        Assert.Equal(35, (await Client.GetFromJsonAsync<PayloadResponse<WeatherCurrentResponse>>($"/api/weather/regions/{region.Id}/current"))!.Data!.Temperature);
        var after = (await Client.GetFromJsonAsync<PayloadResponse<WeatherForecastResponse>>($"/api/weather/regions/{region.Id}/forecast"))!.Data!;
        Assert.All(after.Hourly, item => Assert.Equal(35, item.Temperature));
        var history = (await Client.GetFromJsonAsync<PayloadResponse<PagedResponse<WeatherCurrentResponse>>>($"/api/weather/regions/{region.Id}/history?pageSize=1"))!.Data!;
        Assert.Equal(2, history.TotalItems);
        Assert.Equal(35, Assert.Single(history.Items).Temperature);
        var pageTwo = (await Client.GetFromJsonAsync<PayloadResponse<PagedResponse<WeatherCurrentResponse>>>($"/api/weather/regions/{region.Id}/history?pageSize=1&page=2"))!.Data!;
        Assert.Equal(30, Assert.Single(pageTwo.Items).Temperature);
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        Assert.Equal(72, await context.WeatherForecasts.CountAsync(item => item.GeoRegionId == region.Id));
        Assert.Equal(2, Provider.Calls);
    }

    [Fact]
    public async Task ProviderFailure_PreservesData_AndPersistsRetryAfterCooldown()
    {
        var region = await Region();
        await Authenticate(UserRoles.PlatformAdministrator);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null)).StatusCode);
        await AllowRefresh(region.Id);
        Provider.Fail = true;
        Provider.RetryAt = DateTime.UtcNow.AddHours(2);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/weather/regions/{region.Id}/current")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null)).StatusCode);
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        Assert.Equal(1, await context.WeatherSnapshots.CountAsync(item => item.GeoRegionId == region.Id));
        Assert.Equal(72, await context.WeatherForecasts.CountAsync(item => item.GeoRegionId == region.Id));
        Assert.DoesNotContain(region.Id, await scope.ServiceProvider.GetRequiredService<IWeatherRepository>().GetDueRegions(DateTime.UtcNow.AddHours(1), default));
        var storedRegion = await context.GeoRegions.AsNoTracking().SingleAsync(item => item.Id == region.Id);
        Assert.True(storedRegion.WeatherRequestedAt < DateTime.UtcNow);
        Assert.Equal(Provider.RetryAt!.Value, storedRegion.WeatherRetryAfter!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ConcurrentRefresh_UsesOneProviderCall_AndReleasesDatabaseLock()
    {
        var region = await Region();
        await Authenticate(UserRoles.PlatformAdministrator);
        Provider.Block = true;
        var first = Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null);
        try
        {
            await Provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null)).StatusCode);
        }
        finally { Provider.Release.TrySetResult(); }
        Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
        Assert.Equal(1, Provider.Calls);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null)).StatusCode);
    }

    [Fact]
    public async Task BoundaryChangeDuringRefresh_DiscardsResponse_AndRetainsAdministrativeHistory()
    {
        var region = await Region();
        await Authenticate(UserRoles.PlatformAdministrator);
        await Seed(region, DateTime.UtcNow.AddHours(-3), 29);
        Provider.Block = true;
        var refresh = Client.PostAsync($"/api/weather/regions/{region.Id}/refresh", null);
        try
        {
            await Provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var scope = Factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var tracked = await context.GeoRegions.SingleAsync(item => item.Id == region.Id);
            tracked.Update(tracked.Name, null, Boundary(-23.58));
            await context.SaveChangesAsync();
        }
        finally { Provider.Release.TrySetResult(); }
        Assert.Equal(HttpStatusCode.Conflict, (await refresh).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Client.GetAsync($"/api/weather/regions/{region.Id}/current")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Client.GetAsync($"/api/weather/regions/{region.Id}/forecast")).StatusCode);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var tracked = await context.GeoRegions.SingleAsync(item => item.Id == region.Id);
            tracked.Deactivate();
            await context.SaveChangesAsync();
        }
        var history = (await Client.GetFromJsonAsync<PayloadResponse<PagedResponse<WeatherCurrentResponse>>>($"/api/weather/regions/{region.Id}/history"))!.Data!;
        Assert.Equal(1, Assert.Single(history.Items).BoundaryVersion);
        await Authenticate(UserRoles.Citizen);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/weather/regions/{region.Id}/history")).StatusCode);
    }

    [Fact]
    public async Task Overview_ExcludesStaleAndMissingValuesFromAverages_AndExpiredForecastsAreHidden()
    {
        var current = await Region();
        var stale = await Region();
        var missing = await Region();
        await Seed(current, DateTime.UtcNow, 30);
        await Seed(stale, DateTime.UtcNow.AddHours(-3), 100);
        await Authenticate(UserRoles.Citizen);
        var overview = (await Client.GetFromJsonAsync<PayloadResponse<WeatherOverviewResponse>>("/api/weather/overview"))!.Data!;
        Assert.Equal("CURRENT", overview.Regions.Single(item => item.RegionId == current.Id).DataStatus);
        Assert.Equal("STALE", overview.Regions.Single(item => item.RegionId == stale.Id).DataStatus);
        Assert.Equal("NO_DATA", overview.Regions.Single(item => item.RegionId == missing.Id).DataStatus);
        Assert.Equal(overview.Regions.Where(item => item.DataStatus == "CURRENT").Average(item => item.Temperature), overview.AverageTemperature);
        var staleCurrent = (await Client.GetFromJsonAsync<PayloadResponse<WeatherCurrentResponse>>($"/api/weather/regions/{stale.Id}/current"))!.Data!;
        Assert.Equal("STALE", staleCurrent.DataStatus);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Client.GetAsync($"/api/weather/regions/{stale.Id}/forecast")).StatusCode);
    }

    [Fact]
    public async Task RefreshEndpoint_AppliesAdministrativeRateLimit()
    {
        await Authenticate(UserRoles.PlatformAdministrator);
        for (var attempt = 0; attempt < 10; attempt++)
            Assert.Equal(HttpStatusCode.NotFound, (await Client.PostAsync($"/api/weather/regions/{Guid.NewGuid()}/refresh", null)).StatusCode);
        var rejected = await Client.PostAsync($"/api/weather/regions/{Guid.NewGuid()}/refresh", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Contains("Too many administrative requests", await rejected.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CachedData_RecalculatesStaleness_AndRemovesExpiredForecasts()
    {
        var region = await Region();
        var now = DateTime.UtcNow;
        await Seed(region, now, 30);
        await using var scope = Factory.Services.CreateAsyncScope();
        var regions = scope.ServiceProvider.GetRequiredService<IGeoRegionRepository>();
        var weather = scope.ServiceProvider.GetRequiredService<IWeatherRepository>();
        var settings = Options.Create(new OpenMeteoSettings());
        var current = new QueryWeatherUseCase(regions, weather, settings, new FixedTime(now));
        Assert.Equal("CURRENT", (await current.Current(region.Id, default)).DataStatus);
        Assert.Single((await current.Forecast(region.Id, null, default)).Hourly);
        var later = new QueryWeatherUseCase(regions, weather, settings, new FixedTime(now.AddHours(3)));
        Assert.Equal("STALE", (await later.Current(region.Id, default)).DataStatus);
        Assert.Equal("NO_DATA", (await later.Forecast(region.Id, null, default)).DataStatus);
    }

    [Fact]
    public async Task FailedPersistence_RollsBackBothSnapshotAndForecastChanges()
    {
        var region = await Region();
        var now = DateTime.UtcNow;
        await Seed(region, now, 30);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var invalid = new WeatherObservation(now, now.AddMinutes(1), new(32, 60, 34, 8, 0, 20, 1),
                [new(now.AddHours(1), 32, 60, 34, 15, 8, 20, 8, 780), new(now.AddHours(2), decimal.MaxValue, 60, 34, 15, 8, 20, 8, 780)]);
            await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<IWeatherRepository>().Save(region, -23.588, -46.655, invalid, default));
        }
        await using var verification = Factory.Services.CreateAsyncScope();
        var context = verification.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        Assert.Equal(30, (await context.WeatherSnapshots.SingleAsync(item => item.GeoRegionId == region.Id)).Temperature);
        Assert.Equal(30, (await context.WeatherForecasts.SingleAsync(item => item.GeoRegionId == region.Id)).Temperature);
    }

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private static Polygon Boundary(double latitude = -23.59) => new(new LinearRing([
        new(-46.66, latitude), new(-46.65, latitude), new(-46.65, latitude + .005), new(-46.66, latitude + .005), new(-46.66, latitude)])) { SRID = 4326 };
    private async Task<GeoRegion> Region()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var city = new City("Weather test city", "SP", $"35{Random.Shared.Next(0, 100_000):D5}");
        context.Cities.Add(city);
        var region = new GeoRegion(city.Id, "Weather test region", null, Boundary());
        context.GeoRegions.Add(region);
        await context.SaveChangesAsync();
        return region;
    }
    private async Task AllowRefresh(Guid regionId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>().GeoRegions.Where(item => item.Id == regionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.WeatherRequestedAt, DateTime.UtcNow.AddMinutes(-6)));
    }
    private async Task Seed(GeoRegion region, DateTime fetchedAt, decimal temperature)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var observation = new WeatherObservation(fetchedAt.AddMinutes(-5), fetchedAt, new(temperature, 60, 34, 8, 0, 20, 1),
            [new(fetchedAt.AddHours(1), temperature, 60, 34, 10, 8, 20, 8, 780)]);
        Assert.True(await scope.ServiceProvider.GetRequiredService<IWeatherRepository>().Save(region, -23.588, -46.655, observation, default));
    }
    private async Task Authenticate(UserRoles role)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var user = new User("Weather test", $"{Guid.NewGuid():N}@example.com", "hash");
        user.UpdateRole(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueSession(user));
    }
}
