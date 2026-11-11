using NetTopologySuite.Geometries;
using UrbanShade.Domain.Routing;

namespace WebApi.Tests.Routes;

public sealed class RouteTestProvider : IRoutingProvider
{
    public Task<IReadOnlyList<RouteCandidate>> GetRoutes(RoutePoint origin, RoutePoint destination, CancellationToken cancellationToken)
    {
        var line = new LineString([new(origin.Longitude,origin.Latitude),new(destination.Longitude,destination.Latitude)]) { SRID = 4326 };
        IReadOnlyList<RouteCandidate> routes = [new("route-1",line,1113.2,800,true,"test","test attribution",[])];
        return Task.FromResult(routes);
    }
}
