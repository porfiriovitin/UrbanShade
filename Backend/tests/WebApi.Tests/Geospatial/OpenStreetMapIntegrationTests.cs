using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UrbanShade.Application.UseCases.GreenAreas.Import;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.Security;

namespace WebApi.Tests.Geospatial;

public sealed class OpenStreetMapIntegrationTests : BaseIntegrationTest
{
    public OpenStreetMapIntegrationTests(UrbanShadeApplicationFactory factory) : base(factory) { }
    private static NetTopologySuite.Geometries.Geometry Boundary => GeoJsonGeometry.Read(JsonDocument.Parse("""
        {"type":"Polygon","coordinates":[[[-46.657,-23.589],[-46.654,-23.589],[-46.654,-23.586],[-46.657,-23.586],[-46.657,-23.589]]]}
        """).RootElement);
    private static readonly DateTime Reference = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static OpenStreetMapSnapshot Snapshot(long id) => new(Reference, [new("way", id, "Mapped Park", Boundary, "{\"leisure\":\"park\",\"access\":\"private\"}")]);
    [Fact]
    public async Task AdministrativeEndpoint_IsIndependentOfSatelliteSettings_AndReusesDailyJob()
    {
        var region = await CreateRegion();
        var path = $"/api/geo/regions/{region.Id}/refresh-green-areas";
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsync(path, null)).StatusCode);
        await Authenticate(UserRoles.Citizen);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsync(path, null)).StatusCode);
        await Authenticate(UserRoles.PlatformAdministrator);
        var settings = Factory.Services.GetRequiredService<OpenStreetMapSettings>();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Client.PostAsync(path, null)).StatusCode);
        settings.Enabled = true;
        try
        {
            Assert.False(Factory.Services.GetRequiredService<GeoAnalysisSettings>().Enabled);
            var first = await Client.PostAsync(path, null);
            Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
            var job = (await first.Content.ReadFromJsonAsync<PayloadResponse<GeoJobResponse>>())!.Data!;
            Assert.Equal("OPENSTREETMAP", job.JobType);
            Assert.EndsWith($"/api/geo/jobs/{job.Id}", first.Headers.Location!.ToString());
            var again = await Client.PostAsync(path, null);
            Assert.Equal(job.Id, (await again.Content.ReadFromJsonAsync<PayloadResponse<GeoJobResponse>>())!.Data!.Id);
            await Import(new FakeSource(Snapshot(101)));
            var completed = (await Client.GetFromJsonAsync<PayloadResponse<GeoJobResponse>>($"/api/geo/jobs/{job.Id}"))!.Data!;
            Assert.Equal("COMPLETED", completed.Status);
            var collection = await Client.GetFromJsonAsync<JsonElement>("/api/geo/green-areas?bbox=-46.66,-23.60,-46.65,-23.58");
            Assert.Equal("ODbL-1.0", collection.GetProperty("sources").GetProperty("openStreetMap").GetProperty("license").GetString());
            var feature = collection.GetProperty("features").EnumerateArray().Single(item => item.GetProperty("properties").GetProperty("osmElementId").GetInt64() == 101);
            var properties = feature.GetProperty("properties");
            Assert.Equal("OPENSTREETMAP", properties.GetProperty("source").GetString());
            Assert.Equal("private", properties.GetProperty("sourceTags").GetProperty("access").GetString());
            Assert.Equal("https://www.openstreetmap.org/way/101", properties.GetProperty("osmUrl").GetString());
            Assert.True(properties.GetProperty("estimatedAreaSquareMeters").GetDecimal() > 1000m);
            Assert.NotEqual(JsonValueKind.Null, properties.GetProperty("importedAt").ValueKind);
        }
        finally { settings.Enabled = false; }
    }
    [Fact]
    public async Task OverlappingRegions_ShareSourceObject_AndEmptyRefreshRemovesOnlyOwnLinks()
    {
        var first = await CreateRegion();
        var second = await CreateRegion();
        await Enqueue(first, 0);
        await Import(new FakeSource(Snapshot(202)));
        await Enqueue(second, 0);
        await Import(new FakeSource(Snapshot(202)));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var area = await context.GreenAreas.SingleAsync(area => area.OsmElementId == 202);
            Assert.Equal(2, await context.GreenAreaRegions.CountAsync(link => link.GreenAreaId == area.Id));
        }
        await Enqueue(first, -1);
        await Import(new FakeSource(new(Reference.AddHours(1), [])));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            Assert.False(await context.GreenAreaRegions.AnyAsync(link => link.GeoRegionId == first.Id));
            Assert.Single(await context.GreenAreaRegions.Where(link => link.GeoRegionId == second.Id).ToListAsync());
            var region = await context.GeoRegions.SingleAsync(region => region.Id == second.Id);
            region.Deactivate();
            await context.SaveChangesAsync();
        }
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var areas = await scope.ServiceProvider.GetRequiredService<IGreenAreaRepository>().GetByBounds(Boundary, 1, 100, default);
            Assert.DoesNotContain(areas, area => area.OsmElementId == 202);
        }
    }
    [Fact]
    public async Task ProviderFailure_PreservesStoredAreas_AndFailedJobCanRetry()
    {
        var region = await CreateRegion();
        await Enqueue(region, 0);
        await Import(new FakeSource(Snapshot(303)));
        var job = await Enqueue(region, -1);
        await Import(new FakeSource(null));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            Assert.Single(await context.GreenAreaRegions.Where(link => link.GeoRegionId == region.Id).ToListAsync());
            Assert.Equal(GeoJobStatus.Failed, (await context.GeoAnalysisJobs.SingleAsync(item => item.Id == job.Id)).Status);
            await scope.ServiceProvider.GetRequiredService<IGeoAnalysisJobRepository>().Enqueue(job, default);
        }
        await Import(new FakeSource(Snapshot(303)));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            Assert.Equal(GeoJobStatus.Completed, (await context.GeoAnalysisJobs.SingleAsync(item => item.Id == job.Id)).Status);
            Assert.Equal(1, await context.GreenAreas.CountAsync(area => area.OsmElementId == 303));
        }
    }
    [Fact]
    public async Task Queue_IsolatesSources_RecoversExpiredLease_AndHidesOldBoundaryLinks()
    {
        var region = await CreateRegion();
        var job = await Enqueue(region, 0);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<IGeoAnalysisJobRepository>();
            Assert.Null(await jobs.Claim(default));
            Assert.Equal(job.Id, (await jobs.Claim(default, GeoJobType.OpenStreetMap))!.Id);
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"GeoAnalysisJobs\" SET \"LeaseExpiresAt\" = now() - interval '1 minute' WHERE \"Id\" = {job.Id}");
        }
        await Import(new FakeSource(Snapshot(404)));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            Assert.Equal(2, (await context.GeoAnalysisJobs.SingleAsync(item => item.Id == job.Id)).Attempts);
            var tracked = await context.GeoRegions.SingleAsync(item => item.Id == region.Id);
            var expanded = (NetTopologySuite.Geometries.Geometry)tracked.Boundary.Copy();
            expanded.Coordinates[1].X += 0.0001;
            expanded.GeometryChanged();
            tracked.Update(tracked.Name, null, expanded);
            await context.SaveChangesAsync();
        }
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var areas = await scope.ServiceProvider.GetRequiredService<IGreenAreaRepository>().GetByBounds(Boundary, 1, 100, default);
            Assert.DoesNotContain(areas, area => area.OsmElementId == 404);
        }
    }
    [Fact]
    public async Task SupersededOrOlderSnapshot_CannotEraseNewerImport()
    {
        var region = await CreateRegion();
        var old = await Enqueue(region, 0);
        var newer = await Enqueue(region, -1);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IGreenAreaRepository>();
            var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await using var transaction = await unit.BeginTransaction(default);
            Assert.True(await repository.ImportOpenStreetMap(newer, Snapshot(505), default));
            Assert.False(await repository.ImportOpenStreetMap(old, new(Reference.AddHours(1), []), default));
            Assert.False(await repository.ImportOpenStreetMap(newer, new(Reference.AddHours(-1), []), default));
            await transaction.Commit(default);
        }
        // Finish pending jobs so subsequent tests do not consume another test's queue entries.
        await Import(new FakeSource(Snapshot(505)));
        await Import(new FakeSource(Snapshot(505)));
    }
    private async Task<GeoAnalysisJob> Enqueue(GeoRegion region, int day)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IGeoAnalysisJobRepository>().Enqueue(new GeoAnalysisJob(region,
            DateTime.UtcNow.Date.AddDays(day), DateTime.UtcNow.Date.AddDays(day + 1), "osm-v1", JsonSerializer.Serialize(new OpenStreetMapSettings().Parameters()), GeoJobType.OpenStreetMap), default);
    }
    private async Task Import(IOpenStreetMapService source)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        await new ImportOpenStreetMapUseCase(provider.GetRequiredService<IGeoAnalysisJobRepository>(), provider.GetRequiredService<IGreenAreaRepository>(), source,
            provider.GetRequiredService<IUnitOfWork>(), NullLogger<ImportOpenStreetMapUseCase>.Instance).Execute(default);
    }
    private async Task<GeoRegion> CreateRegion()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var city = await context.Cities.SingleOrDefaultAsync(city => city.IbgeCode == "3550308");
        if (city is null) { city = new City("Sao Paulo", "SP", "3550308"); context.Cities.Add(city); }
        var region = new GeoRegion(city.Id, "OSM Region", null, Boundary);
        context.GeoRegions.Add(region);
        await context.SaveChangesAsync();
        return region;
    }
    private async Task Authenticate(UserRoles role)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var user = new User("OSM Test", Guid.NewGuid().ToString("N") + "@example.com", "hash");
        user.UpdateRole(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueSession(user));
    }
    private sealed class FakeSource(OpenStreetMapSnapshot? snapshot) : IOpenStreetMapService
    {
        public Task<OpenStreetMapSnapshot> GetGreenAreas(NetTopologySuite.Geometries.Geometry boundary, OpenStreetMapParameters parameters, CancellationToken cancellationToken) =>
            snapshot is null ? throw new OpenStreetMapException("OpenStreetMap import timed out. Previous data was preserved.") : Task.FromResult(snapshot);
    }
}
