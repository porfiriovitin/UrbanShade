using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Routes;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Routing;
using UrbanShade.Domain.RegionThermalAnalysis;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.Security;

namespace WebApi.Tests.Routes;

public sealed class RouteIntegrationTests : BaseIntegrationTest
{
    public RouteIntegrationTests(UrbanShadeApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task SpatialSegmentation_ResolvesOverlapAndPreservesRepeatedStreetTraversal()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var city = new City("Routing test", "SP", $"35{Random.Shared.Next(100000):D5}");
        var broad = new GeoRegion(city.Id,"Broad region",null,Boundary(.002,.008));
        var specific = new GeoRegion(city.Id,"Specific region",null,Boundary(.004,.006));
        context.Cities.Add(city);
        context.GeoRegions.AddRange(broad,specific);
        await context.SaveChangesAsync();
        var geometry = new LineString([new(0,0),new(.01,0),new(0,0)]) { SRID = 4326 };
        var route = new RouteCandidate("route",geometry,2226.4,1600,true,"test","test",[]);
        var segments = await scope.ServiceProvider.GetRequiredService<IRouteGeometryRepository>().Analyze([route],default);
        var expected = await context.Database.SqlQuery<double>($"SELECT ST_Length(ST_GeomFromText({geometry.AsText()},4326)::geography) AS \"Value\"").SingleAsync();
        Assert.InRange(Math.Abs(expected - segments.Sum(item => item.DistanceMeters)),0,.01);
        Assert.InRange(segments.Where(item => item.GeoRegionId == specific.Id).Sum(item => item.DistanceMeters),445,446);
        Assert.InRange(segments.Where(item => item.GeoRegionId == broad.Id).Sum(item => item.DistanceMeters),890,891);
        Assert.Contains(segments,item => item.GeoRegionId is null);
        Assert.Contains(segments,item => item.EdgeIndex == 2);
        Assert.All(segments,item => Assert.Null(item.ExposureIndex));
    }

    [Fact]
    public async Task Deduplication_PreservesConventionalRoute_AndDistinctAlternative()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRouteGeometryRepository>();
        RouteCandidate Route(string id,Coordinate[] coordinates,bool conventional = false) => new(id,new LineString(coordinates) { SRID = 4326 },1500,1000,conventional,"test","test",[]);
        var routes = await repository.RemoveDuplicates([
            Route("conventional",[new(0,0),new(.01,0)],true),
            Route("duplicate",[new(0,0),new(.005,0),new(.01,0)]),
            Route("alternative",[new(0,0),new(.005,.005),new(.01,0)])],default);
        Assert.Equal(2,routes.Count);
        Assert.Equal("conventional",routes[0].Id);
        Assert.Equal("alternative",routes[1].Id);
    }

    [Fact]
    public async Task Endpoint_RequiresJwt_ReturnsUnknownCoverage_AndAppliesRateLimit()
    {
        var request = new { origin = new { latitude = 0,longitude = 0 }, destination = new { latitude = 0,longitude = .01 },travelMode = "WALKING" };
        Assert.Equal(HttpStatusCode.Unauthorized,(await Client.PostAsJsonAsync("/api/routes/compare",request)).StatusCode);
        await Authenticate();
        var response = await Client.PostAsJsonAsync("/api/routes/compare",request);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<PayloadResponse<RouteComparisonResponse>>())!.Data!;
        var route = Assert.Single(data.Routes);
        Assert.True(route.IsRecommended);
        Assert.Null(route.ExposureIndex);
        Assert.Equal(0,route.ThermalCoveragePercent);
        Assert.Equal("SINGLE_ROUTE_AVAILABLE",data.RecommendationReason);
        Assert.All(route.Segments,segment => Assert.Equal("UNKNOWN",segment.DataStatus));
        for (var index = 0; index < 9; index++)
            Assert.Equal(HttpStatusCode.BadRequest,(await Client.PostAsJsonAsync("/api/routes/compare",new { origin = new { latitude = 91,longitude = 0 }, destination = new { latitude = 0,longitude = .01 } })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await Client.PostAsJsonAsync("/api/routes/compare",request)).StatusCode);
    }

    [Fact]
    public async Task Endpoint_UsesPersistedRegionalIndex_AndRejectsExpiredSourceDates()
    {
        var now = DateTime.UtcNow;
        Guid analysisId;
        string Metadata(DateTime measuredAt,ExposureResult result,RegionThermalAnalysisSettings settings) => JsonSerializer.Serialize(new
        {
            scores = result.Scores, appliedWeights = result.AppliedWeights, parameters = settings,
            sensors = new[] { new { MeasuredAt = measuredAt } }, vegetation = new { ObservationEndAt = now.AddDays(-1) }
        });
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var city = new City("Thermal route test","SP",$"35{Random.Shared.Next(100000):D5}");
            var region = new GeoRegion(city.Id,"Route coverage",null,Boundary(10,10.01));
            context.Cities.Add(city);
            context.GeoRegions.Add(region);
            var settings = scope.ServiceProvider.GetRequiredService<RegionThermalAnalysisSettings>();
            var input = new ExposureInput(35,null,20,2,2,true,95);
            var result = new ExposureIndexCalculator(settings).Calculate(input);
            var analysis = new RegionThermalAnalysis(new(region,[],null,null,null,null,null),input,result,"1.0","test",Metadata(now.AddMinutes(-1),result,settings),now);
            context.RegionThermalAnalyses.Add(analysis);
            analysisId = analysis.Id;
            await context.SaveChangesAsync();
        }
        await Authenticate();
        var request = new { origin = new { latitude = 0,longitude = 10 },destination = new { latitude = 0,longitude = 10.01 } };
        var response = await Client.PostAsJsonAsync("/api/routes/compare",request);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<PayloadResponse<RouteComparisonResponse>>())!.Data!;
        Assert.Equal("DEMONSTRATION",data.AnalysisMode);
        var route = Assert.Single(data.Routes);
        Assert.Equal(58.57m,route.ExposureIndex);
        Assert.Equal(100,route.ThermalCoveragePercent);
        Assert.True(route.CanRecommendThermally);
        Assert.All(route.Segments,segment => Assert.Equal(analysisId,segment.AnalysisId));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<RegionThermalAnalysisSettings>();
            var result = new ExposureIndexCalculator(settings).Calculate(new(35,null,20,2,2,true,95));
            var expired = Metadata(now.AddHours(-1),result,settings);
            await scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>().RegionThermalAnalyses.Where(item => item.Id == analysisId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.SourceMetadata,expired));
        }
        response = await Client.PostAsJsonAsync("/api/routes/compare",request);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        route = Assert.Single((await response.Content.ReadFromJsonAsync<PayloadResponse<RouteComparisonResponse>>())!.Data!.Routes);
        Assert.Null(route.ExposureIndex);
        Assert.Equal(0,route.ThermalCoveragePercent);
    }

    private static Polygon Boundary(double west,double east) => new(new LinearRing([new(west,-.001),new(east,-.001),new(east,.001),new(west,.001),new(west,-.001)])) { SRID = 4326 };

    private async Task Authenticate()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var user = new User("Routes test",$"{Guid.NewGuid():N}@example.com","hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",IssueSession(user));
    }
}
