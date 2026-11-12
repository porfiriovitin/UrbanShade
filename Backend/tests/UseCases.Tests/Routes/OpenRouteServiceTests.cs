using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UrbanShade.Domain.Routing;
using UrbanShade.Exceptions;
using UrbanShade.Infraestructure.Routing;

namespace UseCases.Tests.Routes;

public sealed class OpenRouteServiceTests
{
    [Fact]
    public async Task Provider_SendsCoordinatesKeyAndOptions_AndCachesGeometry()
    {
        var bodies = new List<JsonElement>();
        var handler = new Handler(async (request, token) =>
        {
            Assert.Equal("test-only-key", Assert.Single(request.Headers.GetValues("Authorization")));
            Assert.EndsWith("/directions/foot-walking/geojson", request.RequestUri!.AbsolutePath);
            bodies.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)).RootElement.Clone());
            return Response();
        });
        var provider = Provider(handler);
        var first = await provider.GetRoutes(new(0,0), new(0,.01), default);
        var cached = await provider.GetRoutes(new(0,0), new(0,.01), default);
        Assert.Equal(3, bodies.Count);
        Assert.Equal(first, cached);
        Assert.True(first[0].IsConventional);
        Assert.All(first.Skip(1), route => Assert.False(route.IsConventional));
        Assert.Equal(.01, bodies[0].GetProperty("coordinates")[1][0].GetDouble());
        Assert.Equal("pt", bodies[0].GetProperty("language").GetString());
        Assert.Equal(3, bodies[1].GetProperty("alternative_routes").GetProperty("target_count").GetInt32());
        Assert.Equal(1, bodies[2].GetProperty("options").GetProperty("profile_params").GetProperty("weightings").GetProperty("green").GetInt32());
        Assert.Equal(4326, first[0].Geometry.SRID);
        Assert.Single(first[0].Instructions);
    }

    [Fact]
    public async Task OptionalFailure_PreservesConventionalRoute()
    {
        var count = 0;
        var routes = await Provider(new Handler((_, _) => Task.FromResult(++count == 1 ? Response() : new HttpResponseMessage(HttpStatusCode.BadRequest))))
            .GetRoutes(new(0,0),new(0,.01),default);
        Assert.True(Assert.Single(routes).IsConventional);
    }

    [Fact]
    public async Task TransientFailure_Retries_WithoutInventingGeometry()
    {
        var count = 0;
        var settings = new RoutingSettings { MaxAlternativeRoutes = 1, MaxRetryAttempts = 1 };
        var routes = await Provider(new Handler((_, _) => Task.FromResult(++count == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Response())),settings)
            .GetRoutes(new(0,0),new(0,.01),default);
        Assert.Single(routes);
        Assert.Equal(2, count);
        var exception = await Assert.ThrowsAsync<RoutingException>(() => Provider(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)))).GetRoutes(new(0,0),new(0,.01),default));
        Assert.Equal(UrbanShade.Exceptions.ErrorKind.Unavailable, exception.GetErrorKind());
    }

    [Fact]
    public async Task TimeoutAndCancellation_AreDistinguished()
    {
        var handler = new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite,token); return Response(); });
        var exception = await Assert.ThrowsAsync<RoutingException>(() => Provider(handler,new RoutingSettings { TimeoutSeconds = 1 }).GetRoutes(new(0,0),new(0,.01),default));
        Assert.Equal(UrbanShade.Exceptions.ErrorKind.Unavailable, exception.GetErrorKind());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Provider(handler).GetRoutes(new(0,0),new(0,.01),cancellation.Token));
    }

    [Fact]
    public void Parser_RejectsInvalidConventionalGeometry()
    {
        using var document = JsonDocument.Parse("""{"type":"FeatureCollection","features":[{"geometry":{"type":"Polygon"}}]}""");
        Assert.Throws<FormatException>(() => new OpenRouteServiceResponseParser(new()).Parse(document.RootElement,true));
    }

    private static OpenRouteServiceProvider Provider(Handler handler, RoutingSettings? settings = null)
    {
        settings ??= new RoutingSettings { MaxRetryAttempts = 0 };
        return new(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }, settings, Options.Create(new OpenRouteServiceOptions { ApiKey = "test-only-key" }),
            new RoutingGeometryCache(), new OpenRouteServiceResponseParser(settings), NullLogger<OpenRouteServiceProvider>.Instance);
    }

    internal static HttpResponseMessage Response() => new(HttpStatusCode.OK) { Content = new StringContent("""
        {"type":"FeatureCollection","metadata":{"attribution":"test attribution"},"features":[{"type":"Feature","geometry":{"type":"LineString","coordinates":[[0,0],[0.01,0]]},
        "properties":{"summary":{"distance":1113,"duration":800},"segments":[{"steps":[{"instruction":"Continue","distance":1113,"duration":800,"way_points":[0,1]}]}]}}]}
        """) };

    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request,cancellationToken);
    }
}
