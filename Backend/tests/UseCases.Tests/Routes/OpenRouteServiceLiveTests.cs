using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UrbanShade.Domain.Routing;
using UrbanShade.Infraestructure.Routing;

namespace UseCases.Tests.Routes;

public sealed class RoutingLiveFactAttribute : FactAttribute
{
    public RoutingLiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("URBANSHADE_ROUTING_LIVE_TEST") != "true")
            Skip = "Set URBANSHADE_ROUTING_LIVE_TEST=true to test the real routing provider.";
    }
}

public sealed class OpenRouteServiceLiveTests
{
    [RoutingLiveFact]
    public async Task RealProvider_ReturnsWalkingGeometryAndNavigation()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName,"UrbanShade.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName,"src","Backend","UrbanShade.API","appsettings.Development.json")),new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var key = document.RootElement.GetProperty("OpenRouteService").GetProperty("Api_Key").GetString();
        Assert.False(string.IsNullOrWhiteSpace(key), "The development routing key is missing.");
        var settings = new RoutingSettings { TimeoutSeconds = 60, MaxRetryAttempts = 0 };
        using var handler = new StatusHandler();
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var cache = new RoutingGeometryCache();
        var provider = new OpenRouteServiceProvider(client,settings,Options.Create(new OpenRouteServiceOptions { ApiKey = key! }),cache,new(settings),NullLogger<OpenRouteServiceProvider>.Instance);
        var routes = await provider.GetRoutes(new(-23.587,-46.656),new(-23.581,-46.650),default);
        Assert.NotEmpty(routes);
        Assert.Equal(3,handler.Statuses.Count);
        Assert.All(handler.Statuses,status => Assert.True((int)status < 400,$"Routing request returned HTTP {(int)status}."));
        Assert.True(routes[0].IsConventional);
        Assert.All(routes, route =>
        {
            Assert.True(route.Geometry.IsValid);
            Assert.True(route.DistanceMeters > 0);
            Assert.NotEmpty(route.Instructions);
            Assert.False(string.IsNullOrWhiteSpace(route.Attribution));
        });
    }

    private sealed class StatusHandler : DelegatingHandler
    {
        public List<System.Net.HttpStatusCode> Statuses { get; } = [];
        public StatusHandler() : base(new HttpClientHandler()) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request,cancellationToken);
            Statuses.Add(response.StatusCode);
            return response;
        }
    }
}
