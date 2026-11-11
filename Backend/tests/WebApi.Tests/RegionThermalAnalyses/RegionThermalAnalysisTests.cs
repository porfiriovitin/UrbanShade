using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.Analyze;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.RegionThermalAnalysis;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Security;
using UrbanShade.Domain.Weather;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.Security;

namespace WebApi.Tests.RegionThermalAnalyses;

public sealed class RegionThermalAnalysisTests : BaseIntegrationTest
{
    public RegionThermalAnalysisTests(UrbanShadeApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Analysis_SelectsOneCurrentReadingPerCoveredSensor_PreservesMetadataAndHistory()
    {
        var region = await Seed();
        await Authenticate(UserRoles.PlatformAdministrator);
        var first = await Analyze(region.Id);
        Assert.Equal(2, first.SensorCount);
        Assert.Equal(35, first.AverageHeatIndex);
        Assert.Equal(58.57m, first.ExposureIndex);
        Assert.Equal("PARTIAL", first.DataQuality);
        Assert.Equal("SIMULATED_SENSORS", first.DataOrigin);
        Assert.Null(first.IsHeatIslandCandidate);
        Assert.Equal(2, first.SourceMetadata.GetProperty("sensors").GetArrayLength());
        Assert.Equal(first.Id, (await Analyze(region.Id)).Id);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            Assert.Equal(1, await context.RegionThermalAnalyses.CountAsync(item => item.GeoRegionId == region.Id));
            await context.SensorReadings.Where(item => context.Sensors.Any(sensor => sensor.Id == item.SensorId && sensor.Code.StartsWith(region.Id.ToString())))
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.MeasuredAt, DateTime.UtcNow.AddHours(-1)));
        }
        var expired = await Analyze(region.Id);
        Assert.NotEqual(first.Id, expired.Id);
        Assert.Equal("INSUFFICIENT_DATA", expired.DataQuality);
        Assert.Null(expired.ExposureIndex);
        await using var verification = Factory.Services.CreateAsyncScope();
        Assert.Equal(2, await verification.ServiceProvider.GetRequiredService<UrbanShadeDbContext>().RegionThermalAnalyses.CountAsync(item => item.GeoRegionId == region.Id));
    }

    [Fact]
    public async Task Api_EnforcesPermissionsAndValidatesQueries()
    {
        var region = await Seed();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync($"/api/thermal/regions/{region.Id}/latest")).StatusCode);
        await Authenticate(UserRoles.Citizen);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsync($"/api/thermal/regions/{region.Id}/analyze", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/thermal/regions/map?bbox=NaN,0,1,1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/thermal/regions/ranking?limit=501")).StatusCode);
        await Authenticate(UserRoles.PlatformAdministrator);
        await Analyze(region.Id);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/thermal/regions/{region.Id}/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/thermal/regions/{region.Id}/history?pageSize=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/thermal/regions/map?bbox=-1,-1,2,2")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/thermal/regions/ranking")).StatusCode);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            (await context.GeoRegions.SingleAsync(item => item.Id == region.Id)).Deactivate();
            await context.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/thermal/regions/{region.Id}/history")).StatusCode);
        await Authenticate(UserRoles.Citizen);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/thermal/regions/{region.Id}/history")).StatusCode);
    }

    [Fact]
    public async Task RegionLock_RejectsConcurrentProcessing_AndReleasesOnDispose()
    {
        var region = await Seed();
        await using var first = Factory.Services.CreateAsyncScope();
        await using var second = Factory.Services.CreateAsyncScope();
        await using (var lease = await first.ServiceProvider.GetRequiredService<IRegionThermalAnalysisRepository>().Acquire(region.Id, default))
        {
            Assert.NotNull(lease);
            Assert.Null(await second.ServiceProvider.GetRequiredService<IRegionThermalAnalysisRepository>().Acquire(region.Id, default));
        }
        await using var next = await second.ServiceProvider.GetRequiredService<IRegionThermalAnalysisRepository>().Acquire(region.Id, default);
        Assert.NotNull(next);
    }

    [Fact]
    public async Task WeatherFallback_UsesHeatIndex_AndSurfaceComparisonRequiresSameAcquisition()
    {
        var region = await Seed();
        Guid referenceAnalysisId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var stored = await context.GeoRegions.SingleAsync(item => item.Id == region.Id);
            var reference = new GeoRegion(region.CityId, "Reference park", null, region.Boundary);
            context.GeoRegions.Add(reference);
            stored.UpdateReference(reference.Id);
            await context.Sensors.Where(item => item.Code.StartsWith(region.Id.ToString())).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, SensorStatus.INACTIVE));
            var now = DateTime.UtcNow;
            context.WeatherSnapshots.Add(new WeatherSnapshot(region.Id, region.BoundaryVersion, .5, .5,
                new WeatherObservation(now.AddMinutes(-1), now, new(32, 60, 99, 0, 0, 0, 0), [])));
            var job = new GeoAnalysisJob(region, now.AddDays(-2), now, "geo-v1", "surface");
            var referenceJob = new GeoAnalysisJob(reference, now.AddDays(-2), now, "geo-v1", "reference");
            context.GeoAnalysisJobs.AddRange(job, referenceJob);
            SatelliteObservation Observation(decimal temperature) => new(SatelliteDataSource.Landsat9, "landsat", "same-acquisition", now.AddDays(-1), temperature, temperature, temperature, 95, null, null, "{}", []);
            context.SatelliteAnalyses.Add(new SatelliteAnalysis(job, GeoAnalysisType.SurfaceTemperature, Observation(40)));
            var other = new SatelliteAnalysis(referenceJob, GeoAnalysisType.SurfaceTemperature, Observation(35));
            context.SatelliteAnalyses.Add(other);
            referenceAnalysisId = other.Id;
            await context.SaveChangesAsync();
        }
        await Authenticate(UserRoles.PlatformAdministrator);
        var result = await Analyze(region.Id);
        Assert.Equal("WEATHER_MODEL", result.DataOrigin);
        Assert.Equal(0, result.SensorCount);
        Assert.Equal(5, result.SurfaceTemperatureAnomaly);
        Assert.True(result.IsHeatIslandCandidate);
        Assert.Equal("COMPLETE", result.DataQuality);
        Assert.Equal(new UrbanShade.Domain.ThermalAnalysis.HeatIndexCalculator().Calculate(32, 60), result.AverageHeatIndex);
        await using (var scope = Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>().SatelliteAnalyses.Where(item => item.Id == referenceAnalysisId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ImageId, "different-acquisition"));
        result = await Analyze(region.Id);
        Assert.Equal("PARTIAL", result.DataQuality);
        Assert.Null(result.SurfaceTemperatureAnomaly);
        Assert.Null(result.IsHeatIslandCandidate);
    }

    private async Task<GeoRegion> Seed()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var city = new City("Thermal test", "SP", $"35{Random.Shared.Next(100000):D5}");
        var polygon = new Polygon(new LinearRing([new(0, 0), new(1, 0), new(1, 1), new(0, 1), new(0, 0)])) { SRID = 4326 };
        var region = new GeoRegion(city.Id, "Thermal region", null, polygon);
        context.Cities.Add(city);
        context.GeoRegions.Add(region);
        var job = new GeoAnalysisJob(region, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), "geo-v1", "test");
        context.GeoAnalysisJobs.Add(job);
        context.SatelliteAnalyses.Add(new SatelliteAnalysis(job, GeoAnalysisType.Vegetation,
            new(SatelliteDataSource.Sentinel2, "sentinel", "image", DateTime.UtcNow.AddDays(-1), .2m, .1m, .3m, 95, 20, 100, "{}", [])));
        for (var index = 0; index < 4; index++)
        {
            var sensor = new Sensor($"{region.Id}-{index}", "Thermal sensor", null, index == 2 ? 2 : index == 1 ? 0 : .5, .5, null);
            context.Sensors.Add(sensor);
            var reading = new SensorReading(sensor.Id, 32, 60, DateTime.UtcNow.AddMinutes(index == 3 ? -60 : -1));
            context.SensorReadings.Add(reading);
            context.ThermalAnalyses.Add(new ThermalAnalysis(reading.Id, 35, ThermalRiskLevel.HIGH, "1.0", DateTime.UtcNow));
            var old = new SensorReading(sensor.Id, 30, 50, DateTime.UtcNow.AddMinutes(-10));
            context.SensorReadings.Add(old);
            context.ThermalAnalyses.Add(new ThermalAnalysis(old.Id, 30, ThermalRiskLevel.MODERATE, "1.0", DateTime.UtcNow));
            if (index == 3) sensor.UpdateStatus(SensorStatus.INACTIVE);
        }
        await context.SaveChangesAsync();
        return region;
    }

    private async Task<RegionThermalAnalysisResponse> Analyze(Guid id)
    {
        var response = await Client.PostAsync($"/api/thermal/regions/{id}/analyze", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PayloadResponse<RegionThermalAnalysisResponse>>())!.Data!;
    }

    private async Task Authenticate(UserRoles role)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var user = new User("Thermal test", $"{Guid.NewGuid():N}@example.com", "hash");
        user.UpdateRole(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtTokenService(new JwtSettings { SigningKey = UrbanShadeApplicationFactory.SigningKey }).CreateAccessToken(user).Value);
    }
}
