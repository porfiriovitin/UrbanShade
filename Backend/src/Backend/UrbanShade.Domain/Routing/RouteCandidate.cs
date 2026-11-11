using NetTopologySuite.Geometries;

namespace UrbanShade.Domain.Routing;

public sealed record RouteCandidate(string Id, LineString Geometry, double DistanceMeters, double DurationSeconds,
    bool IsConventional, string Provider, string Attribution, IReadOnlyList<RouteInstruction> Instructions);

public sealed record RouteInstruction(string Text, double DistanceMeters, double DurationSeconds, int StartPointIndex, int EndPointIndex);
