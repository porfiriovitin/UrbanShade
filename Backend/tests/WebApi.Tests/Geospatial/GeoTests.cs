using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UrbanShade.Application.UseCases.GeoAnalysisJobs.ProcessPending;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.Security;

namespace WebApi.Tests.Geospatial;

public sealed class GeoTests : BaseIntegrationTest
{
    public GeoTests(UrbanShadeApplicationFactory factory) : base(factory) { }
    private static JsonElement Boundary => JsonDocument.Parse("{\"type\":\"Polygon\",\"coordinates\":[[[-46.657,-23.589],[-46.654,-23.589],[-46.654,-23.586],[-46.657,-23.586],[-46.657,-23.589]]]}").RootElement.Clone();
    [Fact]
    public async Task Regions_EnforcePermissions_ValidateGeometry_AndDeactivateLogically()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/geo/regions")).StatusCode);
        var city = await City();
        await Authenticate(UserRoles.Citizen);
        var request = new { cityId = city.Id, name = "Park", boundary = Boundary };
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync("/api/geo/regions", request)).StatusCode);
        await Authenticate(UserRoles.PlatformAdministrator);
        var created = await Client.PostAsJsonAsync("/api/geo/regions", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var region = (await created.Content.ReadFromJsonAsync<PayloadResponse<GeoRegionResponse>>())!.Data!;
        Assert.EndsWith($"/api/geo/regions/{region.Id}", created.Headers.Location!.ToString());
        Assert.Equal(4326, (await Region(region.Id)).Boundary.SRID);
        var invalid = JsonDocument.Parse("{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[1,1],[0,1],[1,0],[0,0]]]}").RootElement;
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/geo/regions", new { cityId = city.Id, name = "Invalid", boundary = invalid })).StatusCode);
        var huge = JsonDocument.Parse("{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[10,0],[10,10],[0,10],[0,0]]]}").RootElement;
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/geo/regions", new { cityId = city.Id, name = "Huge", boundary = huge })).StatusCode);
        var unavailable = (await Client.GetFromJsonAsync<PayloadResponse<VegetationResponse>>($"/api/geo/regions/{region.Id}/vegetation"))!.Data!;
        Assert.Equal("UNAVAILABLE", unavailable.DataStatus);
        Assert.Null(unavailable.MeanNdvi);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/geo/regions?pageSize=1000")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/geo/green-areas?bbox=0,0,20,20")).StatusCode);
        var patched = await Client.PatchAsJsonAsync($"/api/geo/regions/{region.Id}", new { name = "Renamed" });
        Assert.Equal(1, (await patched.Content.ReadFromJsonAsync<PayloadResponse<GeoRegionResponse>>())!.Data!.BoundaryVersion);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/geo/regions/{region.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/geo/regions/{region.Id}")).StatusCode);
        await using var scope = Factory.Services.CreateAsyncScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>().GeoRegions.SingleAsync(item => item.Id == region.Id)).Active);
    }
    [Fact]
    public async Task Refresh_IsDurableAndIdempotent_ProcessesResultsAndSpatialQueries()
    {
        var region = await CreateRegion();
        await Authenticate(UserRoles.PlatformAdministrator);
        var settings = Factory.Services.GetRequiredService<GeoAnalysisSettings>();
        settings.Enabled = true;
        try
        {
            var responses = await Task.WhenAll(Client.PostAsJsonAsync($"/api/geo/regions/{region.Id}/refresh", new { }), Client.PostAsJsonAsync($"/api/geo/regions/{region.Id}/refresh", new { }));
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
            var first = (await responses[0].Content.ReadFromJsonAsync<PayloadResponse<GeoJobResponse>>())!.Data!;
            Assert.Equal(first.Id, (await responses[1].Content.ReadFromJsonAsync<PayloadResponse<GeoJobResponse>>())!.Data!.Id);
            Assert.Equal("PENDING", first.Status);
            await Process(new FakeEarthEngine());
            var job = (await Client.GetFromJsonAsync<PayloadResponse<GeoJobResponse>>($"/api/geo/jobs/{first.Id}"))!.Data!;
            Assert.Equal("COMPLETED", job.Status);
            var vegetation = (await Client.GetFromJsonAsync<PayloadResponse<VegetationResponse>>($"/api/geo/regions/{region.Id}/vegetation"))!.Data!;
            Assert.Equal(0.6m, vegetation.MeanNdvi);
            Assert.Equal(50m, vegetation.VegetationCoveragePercent);
            var thermal = (await Client.GetFromJsonAsync<PayloadResponse<SurfaceTemperatureResponse>>($"/api/geo/regions/{region.Id}/surface-temperature"))!.Data!;
            Assert.Equal(35m, thermal.AverageTemperature);
            Assert.Equal("LANDSAT9", thermal.Source);
            var history = (await Client.GetFromJsonAsync<PayloadResponse<List<SatelliteAnalysisResponse>>>($"/api/geo/regions/{region.Id}/analyses"))!.Data!;
            Assert.Equal(2, history.Count);
            var green = await Client.GetFromJsonAsync<JsonElement>("/api/geo/green-areas?bbox=-46.66,-23.60,-46.65,-23.58");
            Assert.True(green.GetProperty("features").GetArrayLength() > 0);
            var outside = await Client.GetFromJsonAsync<JsonElement>("/api/geo/green-areas?bbox=0,0,0.01,0.01");
            Assert.Empty(outside.GetProperty("features").EnumerateArray());
            await Process(new FakeEarthEngine());
            Assert.Equal(2, (await Client.GetFromJsonAsync<PayloadResponse<List<SatelliteAnalysisResponse>>>($"/api/geo/regions/{region.Id}/analyses"))!.Data!.Count);
            await Authenticate(UserRoles.Citizen);
            Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync($"/api/geo/jobs/{job.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync($"/api/geo/regions/{region.Id}/refresh", new { })).StatusCode);
        }
        finally { settings.Enabled = false; }
    }
    [Fact]
    public async Task FailedSource_PreservesOtherSource_AndRetryDoesNotDuplicateResults()
    {
        var region = await CreateRegion();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<IGeoAnalysisJobRepository>();
            var job = new GeoAnalysisJob(region, DateTime.UtcNow.Date.AddDays(-30), DateTime.UtcNow.Date, "geo-v1", JsonSerializer.Serialize(new GeoAnalysisSettings().Parameters()));
            await jobs.Enqueue(job, default);
        }
        await Process(new FakeEarthEngine(failThermal: true));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var job = await context.GeoAnalysisJobs.SingleAsync(item => item.GeoRegionId == region.Id);
            Assert.Equal(GeoJobStatus.Failed, job.Status);
            Assert.Single(await context.SatelliteAnalyses.Where(item => item.GeoRegionId == region.Id).ToListAsync());
            await scope.ServiceProvider.GetRequiredService<IGeoAnalysisJobRepository>().Enqueue(job, default);
        }
        await Process(new FakeEarthEngine());
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            Assert.Equal(2, await context.SatelliteAnalyses.CountAsync(item => item.GeoRegionId == region.Id));
            Assert.Equal(GeoJobStatus.Completed, (await context.GeoAnalysisJobs.SingleAsync(item => item.GeoRegionId == region.Id)).Status);
        }
    }
    [Fact]
    public async Task ExpiredLease_IsRecovered_ConcurrentWorkersCannotClaimSameJob_AndEmptyImagesAreNotFailures()
    {
        var region = await CreateRegion();
        Guid jobId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<IGeoAnalysisJobRepository>();
            jobId = (await jobs.Enqueue(new GeoAnalysisJob(region, DateTime.UtcNow.Date.AddDays(-20), DateTime.UtcNow.Date, "geo-v1", JsonSerializer.Serialize(new GeoAnalysisSettings().Parameters())), default)).Id;
            Assert.Equal(jobId, (await jobs.Claim(default))!.Id);
            Assert.Null(await jobs.Claim(default));
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"GeoAnalysisJobs\" SET \"LeaseExpiresAt\" = now() - interval '1 minute' WHERE \"Id\" = {jobId}");
        }
        await Process(new FakeEarthEngine(empty: true));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var job = await context.GeoAnalysisJobs.SingleAsync(item => item.Id == jobId);
            Assert.Equal(GeoJobStatus.Completed, job.Status);
            Assert.Equal(2, job.Attempts);
            Assert.Contains("UNAVAILABLE", job.ResultSummary);
            Assert.False(await context.SatelliteAnalyses.AnyAsync(item => item.GeoRegionId == region.Id));
        }
    }
    [Fact]
    public async Task OldObservation_IsOutdated_AndBoundaryChangeRequiresNewAnalysis()
    {
        var region = await CreateRegion();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var job = new GeoAnalysisJob(region, DateTime.UtcNow.Date.AddDays(-100), DateTime.UtcNow.Date.AddDays(-90), "geo-v1", JsonSerializer.Serialize(new GeoAnalysisSettings().Parameters()));
            job.Complete("Vegetation: AVAILABLE");
            context.GeoAnalysisJobs.Add(job);
            context.SatelliteAnalyses.Add(new SatelliteAnalysis(job, GeoAnalysisType.Vegetation, new SatelliteObservation(SatelliteDataSource.Sentinel2, "COPERNICUS/S2_SR_HARMONIZED", "old-image",
                job.ObservationEndAt, 0.4m, 0.1m, 0.8m, 95m, 40m, 1000m, "{}", [])));
            await context.SaveChangesAsync();
        }
        await Authenticate(UserRoles.PlatformAdministrator);
        var previous = (await Client.GetFromJsonAsync<PayloadResponse<VegetationResponse>>($"/api/geo/regions/{region.Id}/vegetation"))!.Data!;
        Assert.Equal("OUTDATED", previous.DataStatus);
        Assert.Equal(0.4m, previous.MeanNdvi);
        var boundary = JsonDocument.Parse(Boundary.GetRawText().Replace("-46.654", "-46.653")).RootElement;
        Assert.Equal(HttpStatusCode.OK, (await Client.PatchAsJsonAsync($"/api/geo/regions/{region.Id}", new { boundary })).StatusCode);
        var current = (await Client.GetFromJsonAsync<PayloadResponse<VegetationResponse>>($"/api/geo/regions/{region.Id}/vegetation"))!.Data!;
        Assert.Equal("UNAVAILABLE", current.DataStatus);
        Assert.Null(current.MeanNdvi);
        Assert.Single((await Client.GetFromJsonAsync<PayloadResponse<List<SatelliteAnalysisResponse>>>($"/api/geo/regions/{region.Id}/analyses"))!.Data!);
    }
    private async Task Process(IEarthEngineService earthEngine)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        await new ProcessPendingGeoAnalysesUseCase(provider.GetRequiredService<IGeoAnalysisJobRepository>(), provider.GetRequiredService<ISatelliteAnalysisRepository>(),
            provider.GetRequiredService<IGreenAreaRepository>(), earthEngine, provider.GetRequiredService<IUnitOfWork>(), NullLogger<ProcessPendingGeoAnalysesUseCase>.Instance).Execute(default);
    }
    private async Task<GeoRegion> Region(Guid id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IGeoRegionRepository>().FindById(id, default))!;
    }
    private async Task<GeoRegion> CreateRegion()
    {
        var city = await City();
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var region = new GeoRegion(city.Id, "Park", null, GeoJsonGeometry.Read(Boundary));
        context.GeoRegions.Add(region);
        await context.SaveChangesAsync();
        return region;
    }
    private async Task<City> City()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var city = await context.Cities.SingleOrDefaultAsync(item => item.IbgeCode == "3550308");
        if (city is not null) return city;
        city = new City("S?o Paulo", "SP", "3550308");
        context.Cities.Add(city);
        await context.SaveChangesAsync();
        return city;
    }
    private async Task Authenticate(UserRoles role)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var user = new User("Geo Test", Guid.NewGuid().ToString("N") + "@example.com", "hash");
        user.UpdateRole(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueSession(user));
    }
    private sealed class FakeEarthEngine(bool failThermal = false, bool empty = false) : IEarthEngineService
    {
        public Task<SatelliteObservation?> Analyze(NetTopologySuite.Geometries.Geometry boundary, DateTime start, DateTime end, GeoAnalysisType type, GeoProcessingParameters parameters, CancellationToken cancellationToken)
        {
            if (failThermal && type == GeoAnalysisType.SurfaceTemperature) throw new EarthEngineException("Satellite provider is temporarily unreachable.", true);
            if (empty) return Task.FromResult<SatelliteObservation?>(null);
            var vegetation = type == GeoAnalysisType.Vegetation;
            return Task.FromResult<SatelliteObservation?>(new(vegetation ? SatelliteDataSource.Sentinel2 : SatelliteDataSource.Landsat9,
                vegetation ? "COPERNICUS/S2_SR_HARMONIZED" : "LANDSAT/LC09/C02/T1_L2", "test-image", end.AddDays(-1), vegetation ? 0.6m : 35m, vegetation ? 0.1m : 30m, vegetation ? 0.9m : 40m,
                90m, vegetation ? 50m : null, vegetation ? 1000m : null, "{}", vegetation ? [new VegetationPolygon(boundary, 1000m)] : []));
        }
    }
}
