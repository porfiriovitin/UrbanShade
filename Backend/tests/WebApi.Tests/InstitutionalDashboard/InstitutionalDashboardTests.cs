using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.Analyze;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Security;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.InstitutionalDashboard;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.RegionThermalAnalysis;
using UrbanShade.Domain.ThermalAnalysis;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.DataAcess.Repositories;
using UrbanShade.Infraestructure.Security;

namespace WebApi.Tests.InstitutionalDashboard;

public sealed class InstitutionalDashboardTests : BaseIntegrationTest
{
    public InstitutionalDashboardTests(UrbanShadeApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Repository_ExecutesAllQueryShapesInPostgis()
    {
        var data = await Seed();
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInstitutionalDashboardRepository>();
        var query = new InstitutionalQuery { OrganizationId = data.Organization.Id, ActorId = data.Manager.Id };
        await repository.GetOverview(query, default);
        await repository.GetSensorsOverview(query, default);
        await repository.GetRegions(query, false, default);
        await repository.GetEnvironment(query, default);
        await repository.GetMap(query with { IncludeSensors = true, IncludeGreenAreas = true }, default);
    }

    [Fact]
    public async Task Repository_EnforcesMembershipEvenWithoutApplicationAccessCheck()
    {
        var data = await Seed();
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInstitutionalDashboardRepository>();
        var query = new InstitutionalQuery { OrganizationId = data.Other.Id, ActorId = data.Manager.Id };
        Assert.Equal(0, (await repository.GetOverview(query, default)).TotalRegions);
        Assert.Equal(0, (await repository.GetSensorsOverview(query, default)).TotalSensors);
        Assert.Empty((await repository.GetRegions(query, false, default)).Items);
        Assert.Empty(await repository.GetMap(query with { IncludeSensors = true, IncludeGreenAreas = true }, default));
        Assert.Empty(await repository.GetReport(query with { StartDate = DateTime.UtcNow.AddDays(-3), EndDate = DateTime.UtcNow }, default));
    }

    [Fact]
    public async Task CachedIndicators_ExpireWithSourceDates_AndOldValuesRemainHistorical()
    {
        var data = await Seed();
        await using var scope = Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var clock = new DashboardClock(DateTimeOffset.UtcNow);
        using var cache = new InstitutionalDashboardCache();
        var repository = new InstitutionalDashboardRepository(services.GetRequiredService<UrbanShadeDbContext>(),
            services.GetRequiredService<RegionThermalAnalysisSettings>(), services.GetRequiredService<ThermalAnalysisSettings>(),
            services.GetRequiredService<GeoAnalysisSettings>(), services.GetRequiredService<InstitutionalDashboardSettings>(), cache,
            clock, NullLogger<InstitutionalDashboardRepository>.Instance);
        var query = new InstitutionalQuery { OrganizationId = data.Organization.Id, ActorId = data.Manager.Id };
        var first = await repository.GetOverview(query, default);
        Assert.Equal(1, first.RegionsWithValidAnalysis);
        Assert.Same(first, await repository.GetOverview(query, default));
        clock.Now = clock.Now.AddHours(1);
        var expired = await repository.GetOverview(query, default);
        Assert.Equal(0, expired.RegionsWithValidAnalysis);
        Assert.Equal(1, expired.StaleRegions);
        Assert.Null(expired.AverageExposureIndex);
        Assert.Equal(0, expired.ExposureSampleCount);
        Assert.Empty((await repository.GetRegions(query, true, default)).Items);
        var history = await repository.GetReport(query with { StartDate = DateTime.UtcNow.AddDays(-3), EndDate = clock.Now.UtcDateTime }, default);
        Assert.Single(history);
        Assert.NotNull(history[0].ExposureIndex);
    }

    [Fact]
    public async Task Trends_KeepAlgorithmVersionsSeparate_AndRankingRejectsObsoleteLatestAnalysis()
    {
        var data = await Seed();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var previous = await context.RegionThermalAnalyses.SingleAsync(analysis => analysis.GeoRegionId == data.Region.Id);
            var source = await scope.ServiceProvider.GetRequiredService<IRegionThermalAnalysisRepository>().GetData(data.Region.Id, DateTime.UtcNow, default);
            var input = new ExposureInput(35, 5, 20, 1, 1, true, 95);
            var result = scope.ServiceProvider.GetRequiredService<ExposureIndexCalculator>().Calculate(input);
            context.RegionThermalAnalyses.Add(new(source!, input, result, "legacy", new string('a', 64), previous.SourceMetadata, DateTime.UtcNow));
            await context.SaveChangesAsync();
        }
        Assert.Empty(await Get<List<InstitutionalRegionResponse>>("regions/ranking", data.Organization.Id));
        var trends = await Get<InstitutionalTrendsResponse>($"regions/{data.Region.Id}/trends", data.Organization.Id, Period());
        Assert.Equal(2, trends.Series.Select(point => point.AlgorithmVersion).Distinct().Count());
        Assert.All(trends.Series, point => Assert.Equal(1, point.SampleCount));
        var detail = await Get<InstitutionalRegionResponse>($"regions/{data.Region.Id}", data.Organization.Id);
        Assert.True(detail.IsStale);
        Assert.Equal("INSUFFICIENT_DATA", detail.ExposureLevel);
    }

    [Fact]
    public async Task Limits_RejectOversizedExportsAndMaps()
    {
        var data = await Seed();
        await using var scope = Factory.Services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<InstitutionalDashboardSettings>();
        var originalExportLimit = settings.MaxExportRows;
        var originalMapLimit = settings.MaxMapFeatures;
        try
        {
            settings.MaxExportRows = 0;
            settings.MaxMapFeatures = 1;
            Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/institutional/reports/export?organizationId={data.Organization.Id}{Period()}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/institutional/map?organizationId={data.Organization.Id}")).StatusCode);
        }
        finally
        {
            settings.MaxExportRows = originalExportLimit;
            settings.MaxMapFeatures = originalMapLimit;
        }
    }

    [Fact]
    public async Task Overview_UsesInstitutionScope_DistinctSensorsAndLatestReadings()
    {
        var data = await Seed();
        var overview = await Get<InstitutionalOverviewResponse>("overview", data.Organization.Id);
        Assert.Equal(2, overview.TotalRegions);
        Assert.Equal(3, overview.TotalSensors);
        Assert.Equal(1, overview.ActiveSensors);
        Assert.Equal(1, overview.InactiveSensors);
        Assert.Equal(1, overview.MaintenanceSensors);
        Assert.Equal(32, overview.AverageTemperature.Value);
        Assert.Equal(1, overview.AverageTemperature.SampleCount);
        Assert.Equal(1, overview.RegionsWithValidAnalysis);
        Assert.Equal(1, overview.RegionsWithInsufficientData);
        Assert.Equal("DEMONSTRATION", overview.AnalysisMode);
        var distribution = await Get<InstitutionalDistributionResponse>("exposure-distribution", data.Organization.Id);
        Assert.Equal(2, distribution.Low + distribution.Moderate + distribution.High + distribution.Critical + distribution.InsufficientData);
        var ranking = await Get<List<InstitutionalRegionResponse>>("regions/ranking", data.Organization.Id);
        Assert.Single(ranking);
        Assert.Equal(data.Region.Id, ranking[0].RegionId);
        Assert.False(ranking[0].SourceMetadata.TryGetProperty("sensors", out _));
        var sensors = await Get<InstitutionalSensorsResponse>("sensors/overview", data.Organization.Id);
        Assert.Equal(1, sensors.SensorsWithCurrentData);
        Assert.Equal(1, sensors.SensorsWithStaleData);
        Assert.Equal(1, sensors.SensorsWithoutReadings);
        Assert.Equal(2, sensors.Regions.Count);
        var detail = await Get<InstitutionalRegionResponse>($"regions/{data.Region.Id}", data.Organization.Id);
        Assert.Equal(32, detail.AverageTemperature!.Value);
        Assert.NotNull(detail.Geometry);
    }

    [Fact]
    public async Task Access_DeniesThirdPartyIds_AndRevocationOverridesWarmCache()
    {
        var data = await Seed();
        await Get<InstitutionalOverviewResponse>("overview", data.Organization.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/institutional/overview?organizationId={data.Other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/institutional/regions/{data.PrivateRegion.Id}?organizationId={data.Organization.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/institutional/map?organizationId={data.Organization.Id}&regionId={data.PrivateRegion.Id}")).StatusCode);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var member = await context.OrganizationMembers.SingleAsync(member => member.OrganizationId == data.Organization.Id && member.UserId == data.Manager.Id);
            member.Deactivate();
            await context.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/institutional/overview?organizationId={data.Organization.Id}")).StatusCode);
    }

    [Fact]
    public async Task RegionRevocation_ChangesCachedScope_AndMapDoesNotExposeThirdPartySensors()
    {
        var data = await Seed();
        await Get<InstitutionalOverviewResponse>("overview", data.Organization.Id);
        var map = await Get<InstitutionalMapResponse>("map", data.Organization.Id, "&includeSensors=true&includeGreenAreas=true&bbox=0,0,1.6,1.1");
        Assert.Equal("FeatureCollection", map.Type);
        Assert.DoesNotContain(map.Features, feature => feature.Id == data.ThirdPartySensor.Id || feature.Id == data.PrivateRegion.Id);
        var green = Assert.Single(map.Features, feature => feature.Properties.TryGetProperty("source", out _));
        Assert.All(green.Geometry.GetProperty("coordinates")[0].EnumerateArray(), coordinate => Assert.True(coordinate[0].GetDouble() >= 0));
        Assert.Equal(3, map.Features.Count(feature => feature.Properties.TryGetProperty("dataOrigin", out var origin) && origin.GetString() == "SIMULATED_SENSORS" && feature.Properties.TryGetProperty("status", out _)));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var link = await context.OrganizationRegions.SingleAsync(link => link.OrganizationId == data.Organization.Id && link.GeoRegionId == data.Region.Id);
            link.Deactivate();
            await context.SaveChangesAsync();
        }
        var overview = await Get<InstitutionalOverviewResponse>("overview", data.Organization.Id);
        Assert.Equal(1, overview.TotalRegions);
        Assert.Null(overview.AverageExposureIndex.Value);
        Assert.Equal(0, overview.AverageExposureIndex.SampleCount);
    }

    [Fact]
    public async Task Environment_UnionsOverlappingRegions_AndUsesOneContributionPerArea()
    {
        var data = await Seed();
        var environment = await Get<InstitutionalEnvironmentResponse>("environment", data.Organization.Id);
        Assert.NotNull(environment.TotalAreaSquareMeters);
        Assert.NotNull(environment.VegetationSampleAreaSquareMeters);
        Assert.Equal(environment.TotalAreaSquareMeters!.Value, environment.VegetationSampleAreaSquareMeters!.Value, 0);
        Assert.InRange(environment.AverageVegetationCoveragePercent!.Value, 39.9, 40.1);
        Assert.Equal(2, environment.VegetationSampleCount);
        Assert.Equal("HISTORICAL_ENVIRONMENTAL_CONTEXT", environment.DataContext);
        Assert.True(environment.LastObservationAt < DateTime.UtcNow.AddHours(-12));
        Assert.Equal(1, environment.SurfaceSampleCount);
    }

    [Theory]
    [InlineData("HOUR")]
    [InlineData("DAY")]
    [InlineData("WEEK")]
    public async Task Trends_AggregateRealObservations_WithoutRepeatingSatelliteValues(string interval)
    {
        var data = await Seed();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var original = await context.SatelliteAnalyses.SingleAsync(analysis => analysis.GeoRegionId == data.Region.Id && analysis.AnalysisType == GeoAnalysisType.Vegetation);
            var job = new GeoAnalysisJob(data.Region, original.ObservationStartAt, original.ObservationEndAt, original.AlgorithmVersion, "reprocessed-same-image");
            context.GeoAnalysisJobs.Add(job);
            context.SatelliteAnalyses.Add(new(job, GeoAnalysisType.Vegetation,
                new(original.DataSource, original.DatasetId, original.ImageId, original.ObservationEndAt, .2m, .1m, .3m, 95, 20, 100, "{}", [])));
            await context.SaveChangesAsync();
        }
        var dates = Period();
        foreach (var metric in new[] { "EXPOSURE_INDEX", "TEMPERATURE", "HEAT_INDEX", "SURFACE_TEMPERATURE", "VEGETATION_COVERAGE" })
        {
            var response = await Get<InstitutionalTrendsResponse>($"regions/{data.Region.Id}/trends", data.Organization.Id,
                $"{dates}&interval={interval}&metric={metric}");
            Assert.Equal("America/Sao_Paulo", response.TimeZoneId);
            Assert.NotEmpty(response.Series);
            if (metric is "SURFACE_TEMPERATURE" or "VEGETATION_COVERAGE") Assert.Equal(1, Assert.Single(response.Series).SampleCount);
            Assert.All(response.Series, point => Assert.True(point.SampleCount > 0));
        }
    }

    [Fact]
    public async Task Trends_GroupUtcObservationsUsingInstitutionTimeZone()
    {
        var data = await Seed();
        var observed = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            await context.SensorReadings.Where(reading => context.Sensors.Any(sensor => sensor.Id == reading.SensorId && sensor.OrganizationId == data.Organization.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(reading => reading.MeasuredAt, observed));
        }
        var trends = await Get<InstitutionalTrendsResponse>($"regions/{data.Region.Id}/trends", data.Organization.Id, Period() + "&metric=TEMPERATURE&interval=DAY");
        Assert.Equal(observed.AddHours(-3).Date, Assert.Single(trends.Series).LocalDate);
    }

    [Fact]
    public async Task ListCompareAndCsv_RespectPaginationScopeAndFormulaEscaping()
    {
        var data = await Seed();
        var page = await Get<PagedResponse<InstitutionalRegionResponse>>("regions", data.Organization.Id, "&pageSize=1");
        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalItems);
        var comparison = await Get<List<InstitutionalRegionResponse>>("regions/compare", data.Organization.Id, $"&regionIds={data.Region.Id}&regionIds={data.EmptyRegion.Id}");
        Assert.Equal(2, comparison.Count);
        Assert.Contains(comparison, region => region.ExposureLevel == "INSUFFICIENT_DATA" && region.ExposureIndex is null);
        var csv = await Client.GetAsync($"/api/institutional/reports/export?organizationId={data.Organization.Id}{Period()}");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var content = await csv.Content.ReadAsStringAsync();
        Assert.Contains("\"'=Main region\"", content);
        Assert.DoesNotContain(data.PrivateRegion.Id.ToString(), content);
        Assert.DoesNotContain(data.ThirdPartySensor.Id.ToString(), content);
        Assert.DoesNotContain("sensorId", content);
    }

    [Theory]
    [InlineData("&bbox=NaN,0,1,1")]
    [InlineData("&bbox=0,0,30,30")]
    [InlineData("&pageSize=101")]
    [InlineData("&interval=MONTH")]
    [InlineData("&metric=UNKNOWN")]
    [InlineData("&startDate=2026-11-01T00:00:00Z")]
    [InlineData("&startDate=2026-11-01T00:00:00Z&endDate=2026-10-01T00:00:00Z")]
    public async Task Queries_RejectInvalidFilters(string filters)
    {
        var data = await Seed();
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/institutional/map?organizationId={data.Organization.Id}{filters}")).StatusCode);
    }

    [Fact]
    public async Task Policies_RejectCitizensAndFirstAccess_AndAllowOnlyAdminToAssignRegions()
    {
        var data = await Seed();
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsync($"/api/organizations/{data.Organization.Id}/regions/{data.PrivateRegion.Id}", null)).StatusCode);
        var citizen = new User("Citizen", $"{Guid.NewGuid():N}@example.com", "hash");
        await SaveUser(citizen);
        Authenticate(citizen);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync($"/api/institutional/overview?organizationId={data.Organization.Id}")).StatusCode);
        citizen.UpdateRole(UserRoles.PlatformAdministrator);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            (await context.Users.SingleAsync(user => user.Id == citizen.Id)).UpdateRole(UserRoles.PlatformAdministrator);
            await context.SaveChangesAsync();
        }
        Authenticate(citizen);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync($"/api/organizations/{data.Organization.Id}/regions/{data.PrivateRegion.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/organizations/{data.Organization.Id}/regions/{data.PrivateRegion.Id}")).StatusCode);
        citizen.SetTemporaryPassword("temporary-hash", DateTime.UtcNow.AddHours(1));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            (await context.Users.SingleAsync(user => user.Id == citizen.Id)).SetTemporaryPassword("temporary-hash", citizen.TemporaryPasswordExpiresAt!.Value);
            await context.SaveChangesAsync();
        }
        Client.DefaultRequestHeaders.Authorization = new("Bearer", TokenService().CreateFirstAccessToken(citizen).Value);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync($"/api/institutional/overview?organizationId={data.Organization.Id}")).StatusCode);
    }

    private async Task<T> Get<T>(string path, Guid organizationId, string filters = "")
    {
        var response = await Client.GetAsync($"/api/institutional/{path}?organizationId={organizationId}{filters}");
        Assert.True(response.IsSuccessStatusCode, $"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<PayloadResponse<T>>())!.Data!;
    }

    private static string Period() => $"&startDate={Uri.EscapeDataString(DateTime.UtcNow.AddDays(-3).ToString("O"))}&endDate={Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(1).ToString("O"))}";
    private static JwtTokenService TokenService() => new(new JwtSettings { SigningKey = UrbanShadeApplicationFactory.SigningKey });
    private void Authenticate(User user) => Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueSession(user));

    private async Task SaveUser(User user)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        context.Users.Add(user);
        await context.SaveChangesAsync();
    }

    private static Polygon Boundary(double x) => new(new LinearRing([new(x, 0), new(x + 1, 0), new(x + 1, 1), new(x, 1), new(x, 0)])) { SRID = 4326 };

    private async Task<SeedData> Seed()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var manager = new User("Dashboard manager", $"{Guid.NewGuid():N}@example.com", "hash");
        manager.UpdateRole(UserRoles.Manager);
        var organization = new Organization("Dashboard", OrganizationType.Municipality, Guid.NewGuid().ToString("N")[..20], "contact@example.com", null);
        var other = new Organization("Other", OrganizationType.Company, Guid.NewGuid().ToString("N")[..20], "other@example.com", null);
        var city = new City("Dashboard city", "SP", $"35{Random.Shared.Next(100000):D5}");
        var region = new GeoRegion(city.Id, "=Main region", null, Boundary(0));
        var empty = new GeoRegion(city.Id, "Overlapping region", null, Boundary(.5));
        var privateRegion = new GeoRegion(city.Id, "Private region", null, Boundary(3));
        var reference = new GeoRegion(city.Id, "Reference", null, Boundary(5));
        region.UpdateReference(reference.Id);
        context.Users.Add(manager);
        context.Organizations.AddRange(organization, other);
        context.OrganizationMembers.Add(new(organization.Id, manager.Id));
        context.Cities.Add(city);
        context.GeoRegions.AddRange(region, empty, privateRegion, reference);
        context.OrganizationRegions.AddRange(new(organization.Id, region.Id), new(organization.Id, empty.Id), new(other.Id, privateRegion.Id));
        var now = DateTime.UtcNow;
        var active = new Sensor(Guid.NewGuid().ToString("N"), "Own active", null, .5, .75, null);
        var inactive = new Sensor(Guid.NewGuid().ToString("N"), "Own inactive", null, .5, .75, null);
        inactive.UpdateStatus(SensorStatus.INACTIVE);
        var maintenance = new Sensor(Guid.NewGuid().ToString("N"), "Own maintenance", null, .5, .75, null);
        maintenance.UpdateStatus(SensorStatus.MAINTENANCE);
        foreach (var sensor in new[] { active, inactive, maintenance }) sensor.AssignOrganization(organization.Id);
        var thirdParty = new Sensor(Guid.NewGuid().ToString("N"), "Third party", null, .5, .75, null);
        thirdParty.AssignOrganization(other.Id);
        context.Sensors.AddRange(active, inactive, maintenance, thirdParty);
        foreach (var item in new[] { (active, 32m, now.AddMinutes(-1)), (active, 20m, now.AddMinutes(-10)), (maintenance, 40m, now.AddHours(-1)), (thirdParty, 38m, now.AddMinutes(-1)) })
        {
            var reading = new SensorReading(item.Item1.Id, item.Item2, 60, item.Item3);
            context.SensorReadings.Add(reading);
            context.ThermalAnalyses.Add(new(reading.Id, item.Item2 + 3, ThermalRiskLevel.HIGH, "1.0", now));
        }
        foreach (var item in new[] { (region, 20m, now.AddDays(-1)), (empty, 80m, now.AddDays(-2)), (reference, 70m, now.AddDays(-1)) })
        {
            var job = new GeoAnalysisJob(item.Item1, item.Item3.AddHours(-1), item.Item3, "geo-v1", "dashboard-test");
            context.GeoAnalysisJobs.Add(job);
            var vegetation = new SatelliteAnalysis(job, GeoAnalysisType.Vegetation,
                new(SatelliteDataSource.Sentinel2, "sentinel", "vegetation", item.Item3, .2m, .1m, .3m, 95, item.Item2, 100, "{}", []));
            context.SatelliteAnalyses.Add(vegetation);
            if (item.Item1 == region)
                context.GreenAreas.Add(new(region.Id, vegetation.Id, Boundary(-.25), 100, item.Item3));
            if (item.Item1 != empty)
                context.SatelliteAnalyses.Add(new(job, GeoAnalysisType.SurfaceTemperature,
                    new(SatelliteDataSource.Landsat8, "landsat", "same-acquisition", item.Item3, item.Item1 == reference ? 35 : 40, 30, 45, 95, null, null, "{}", [])));
        }
        await context.SaveChangesAsync();
        var analysis = await scope.ServiceProvider.GetRequiredService<IAnalyzeRegionThermalUseCase>().Execute(region.Id);
        Assert.Equal("COMPLETE", analysis.DataQuality);
        Authenticate(manager);
        return new(manager, organization, other, region, empty, privateRegion, thirdParty);
    }

    private sealed record SeedData(User Manager, Organization Organization, Organization Other, GeoRegion Region, GeoRegion EmptyRegion, GeoRegion PrivateRegion, Sensor ThirdPartySensor);

    private sealed class DashboardClock : TimeProvider
    {
        public DashboardClock(DateTimeOffset now) { Now = now; }
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
