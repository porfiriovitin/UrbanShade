using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Infraestructure.Geospatial;
using Xunit.Abstractions;

namespace UseCases.Tests.Geospatial;

public sealed class OpenStreetMapTests
{
    private static readonly OpenStreetMapParameters Parameters = new(2000, 20000, 16 * 1024 * 1024);
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();
    public static string Snapshot(string elements) => "{\"osm3s\":{\"timestamp_osm_base\":\"2026-11-10T12:00:00Z\"},\"elements\":" + elements + "}";
    private const string Way = """
        {"type":"way","id":123,"tags":{"leisure":"park","name":"Test Park","access":"private"},
         "geometry":[{"lon":0,"lat":0},{"lon":1,"lat":0},{"lon":1,"lat":1},{"lon":0,"lat":1},{"lon":0,"lat":0}]}
        """;
    [Fact]
    public void ClosedWay_PreservesIdentityNameAccessAndSourceTimestamp()
    {
        var result = OverpassResponseParser.Parse(Json(Snapshot("[" + Way + "]")), Parameters);
        var area = Assert.Single(result.Areas);
        Assert.Equal("way", area.ElementType);
        Assert.Equal(123, area.ElementId);
        Assert.Equal("Test Park", area.Name);
        Assert.Equal("private", Json(area.Tags).GetProperty("access").GetString());
        Assert.Equal(4326, area.Geometry.SRID);
        Assert.True(area.Geometry.IsValid);
        Assert.Equal(DateTimeKind.Utc, result.ReferenceDate.Kind);
    }
    [Fact]
    public void Multipolygon_MergesReversedSegments_AssignsHoles_AndSuppressesMemberWay()
    {
        var elements = """
        [
          {"type":"relation","id":456,"tags":{"type":"multipolygon","leisure":"garden"},"members":[
            {"type":"way","ref":123,"role":"outer","geometry":[{"lon":0,"lat":0},{"lon":10,"lat":0},{"lon":10,"lat":10}]},
            {"type":"way","ref":124,"role":"outer","geometry":[{"lon":0,"lat":0},{"lon":0,"lat":10},{"lon":10,"lat":10}]},
            {"type":"way","ref":125,"role":"inner","geometry":[{"lon":2,"lat":2},{"lon":4,"lat":2},{"lon":4,"lat":4},{"lon":2,"lat":4},{"lon":2,"lat":2}]}
          ]},
          {"type":"way","id":123,"tags":{"leisure":"park"},"geometry":[{"lon":0,"lat":0},{"lon":10,"lat":0},{"lon":10,"lat":10},{"lon":0,"lat":10},{"lon":0,"lat":0}]}
        ]
        """;
        var area = Assert.Single(OverpassResponseParser.Parse(Json(Snapshot(elements)), Parameters).Areas);
        Assert.Equal("relation", area.ElementType);
        Assert.Equal(456, area.ElementId);
        Assert.Equal(96, area.Geometry.Area);
        Assert.True(area.Geometry.IsValid);
    }
    [Fact]
    public void InvalidGeometryAndExceededLimits_FailWithoutTreatingResponseAsEmpty()
    {
        Assert.Throws<OpenStreetMapException>(() => OverpassResponseParser.Parse(Json(Snapshot("[" + Way.Replace("\"lon\":1,\"lat\":0", "\"lon\":181,\"lat\":0") + "]")), Parameters));
        Assert.Throws<OpenStreetMapException>(() => OverpassResponseParser.Parse(Json(Snapshot("[" + Way + "]")), Parameters with { MaximumVertices = 4 }));
        Assert.Throws<OpenStreetMapException>(() => OverpassResponseParser.Parse(Json(Snapshot("[" + Way + "," + Way + "]")), Parameters with { MaximumElements = 1 }));
        Assert.Throws<OpenStreetMapException>(() => OverpassResponseParser.Parse(Json("{\"remark\":\"runtime error: timeout\",\"elements\":[]}"), Parameters));
    }
    [Fact]
    public void EmptyValidSnapshot_IsAccepted_AndUnrelatedFeaturesAreIgnored()
    {
        Assert.Empty(OverpassResponseParser.Parse(Json(Snapshot("[]")), Parameters).Areas);
        Assert.Empty(OverpassResponseParser.Parse(Json(Snapshot("[" + Way.Replace("\"park\"", "\"pitch\"") + "]")), Parameters).Areas);
        Assert.Empty(OverpassResponseParser.Parse(Json(Snapshot("[" + Way.Replace("\"leisure\":\"park\"", "\"leisure\":\"park\",\"area\":\"no\"") + "]")), Parameters).Areas);
    }
    [Fact]
    public void Query_UsesLatitudeLongitudeWithInvariantDecimals_AndSearchesContainingAreas()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var query = OverpassService.BuildQuery(GeoJsonGeometry.Read(GeoExpressionTests.Boundary));
            Assert.Contains("(-23.589,-46.657,-23.586,-46.654)", query);
            Assert.Contains("pivot.containing", query);
            Assert.Contains("out body geom", query);
            Assert.DoesNotContain("node[", query);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Fact]
    public async Task HttpClient_AcceptsEmptyResult_AndUsesOneBoundedPostRequest()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Snapshot("[]")) });
        var result = await new OverpassService(new HttpClient(handler)).GetGreenAreas(GeoJsonGeometry.Read(GeoExpressionTests.Boundary), Parameters, default);
        Assert.Empty(result.Areas);
        Assert.Equal(1, handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(OverpassService.Endpoint, handler.Url);
        Assert.Contains("data=", handler.Body);
    }
    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    public async Task PermanentHttpFailure_IsSanitizedAndNotRetried(int status)
    {
        var handler = new Handler(_ => new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("internal-provider-error") });
        var error = await Assert.ThrowsAsync<OpenStreetMapException>(() => new OverpassService(new HttpClient(handler)).GetGreenAreas(GeoJsonGeometry.Read(GeoExpressionTests.Boundary), Parameters, default));
        Assert.Equal(1, handler.Requests);
        Assert.DoesNotContain("internal-provider-error", error.Message);
    }
    [Fact]
    public async Task RateLimit_IsRetried_AndResponseSizeIsEnforced()
    {
        var handler = new Handler(attempt =>
        {
            if (attempt > 1) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Snapshot("[]")) };
            var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return limited;
        });
        await new OverpassService(new HttpClient(handler)).GetGreenAreas(GeoJsonGeometry.Read(GeoExpressionTests.Boundary), Parameters, default);
        Assert.Equal(2, handler.Requests);
        var oversized = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 2048)) });
        await Assert.ThrowsAsync<OpenStreetMapException>(() => new OverpassService(new HttpClient(oversized)).GetGreenAreas(GeoJsonGeometry.Read(GeoExpressionTests.Boundary), Parameters with { MaximumResponseBytes = 1024 }, default));
    }
    private sealed class Handler(Func<int, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Url { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Method = request.Method;
            Url = request.RequestUri!.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return response(Requests);
        }
    }
}

public sealed class OpenStreetMapLiveFactAttribute : FactAttribute
{
    public OpenStreetMapLiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("URBANSHADE_OSM_TEST") != "true") Skip = "Set URBANSHADE_OSM_TEST=true to query the public Overpass service.";
    }
}

public sealed class OpenStreetMapLiveTests(ITestOutputHelper output)
{
    [OpenStreetMapLiveFact]
    public async Task RealOverpass_ReturnsValidGreenPolygonsForSmallRegion()
    {
        var result = await new OverpassService(new HttpClient()).GetGreenAreas(GeoJsonGeometry.Read(GeoExpressionTests.Boundary), new OpenStreetMapSettings().Parameters(), default);
        Assert.NotEmpty(result.Areas);
        Assert.All(result.Areas, area => { Assert.True(area.Geometry.IsValid); Assert.Equal(4326, area.Geometry.SRID); Assert.True(area.ElementId > 0); });
        output.WriteLine($"OpenStreetMap snapshot {result.ReferenceDate:O}: {result.Areas.Count} green areas, {result.Areas.Sum(area => area.Geometry.NumPoints)} vertices.");
    }
}
