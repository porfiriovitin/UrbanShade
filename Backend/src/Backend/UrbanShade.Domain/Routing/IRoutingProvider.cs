namespace UrbanShade.Domain.Routing;

public interface IRoutingProvider
{
    Task<IReadOnlyList<RouteCandidate>> GetRoutes(RoutePoint origin, RoutePoint destination, CancellationToken cancellationToken);
}

public sealed record RoutePoint(double Latitude, double Longitude);
