using UrbanShade.Domain.Routing;

namespace UrbanShade.Domain.Repositories;

public interface IRouteGeometryRepository
{
    Task<IReadOnlyList<RouteThermalSegment>> Analyze(IReadOnlyList<RouteCandidate> routes, CancellationToken cancellationToken);
    Task<IReadOnlyList<RouteCandidate>> RemoveDuplicates(IReadOnlyList<RouteCandidate> routes, CancellationToken cancellationToken);
}
