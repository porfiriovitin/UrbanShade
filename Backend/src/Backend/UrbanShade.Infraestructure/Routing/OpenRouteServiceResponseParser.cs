using System.Text.Json;
using NetTopologySuite.Geometries;
using UrbanShade.Domain.Routing;

namespace UrbanShade.Infraestructure.Routing;

public sealed class OpenRouteServiceResponseParser
{
    private readonly RoutingSettings _settings;

    public OpenRouteServiceResponseParser(RoutingSettings settings)
    {
        _settings = settings;
    }

    public IReadOnlyList<RouteCandidate> Parse(JsonElement root, bool conventional)
    {
        if (root.GetProperty("type").GetString() != "FeatureCollection")
            throw new FormatException("Invalid route collection.");

        var attribution = root.TryGetProperty("metadata", out var metadata) && metadata.TryGetProperty("attribution", out var credit) ? credit.GetString() ?? "openrouteservice.org | OpenStreetMap contributors" : "openrouteservice.org | OpenStreetMap contributors";
        var routes = new List<RouteCandidate>();
        var index = 0;

        foreach (var feature in root.GetProperty("features").EnumerateArray().Take(_settings.MaxAlternativeRoutes))
        {
            try
            {
                var geometry = feature.GetProperty("geometry");
                if (geometry.GetProperty("type").GetString() != "LineString")
                    throw new FormatException("A LineString route is required.");

                var coordinates = geometry.GetProperty("coordinates");
                if (coordinates.GetArrayLength() < 2 || coordinates.GetArrayLength() > _settings.MaximumGeometryPoints)
                    throw new FormatException("Route geometry exceeds configured limits.");

                var points = coordinates.EnumerateArray().Select(point =>
                {
                    var longitude = point[0].GetDouble();
                    var latitude = point[1].GetDouble();
                    if (!double.IsFinite(longitude) || !double.IsFinite(latitude) || longitude is < -180 or > 180 || latitude is < -90 or > 90)
                        throw new FormatException("Invalid route coordinates.");

                    return new Coordinate(longitude, latitude);
                }).ToArray();
                var line = new LineString(points)
                {
                    SRID = 4326
                };
                if (line.IsEmpty || !line.IsValid || line.Length <= 0)
                    throw new FormatException("Invalid route geometry.");

                var properties = feature.GetProperty("properties");
                var summary = properties.GetProperty("summary");
                var distance = summary.GetProperty("distance").GetDouble();
                var duration = summary.GetProperty("duration").GetDouble();
                if (!double.IsFinite(distance) || !double.IsFinite(duration) || distance <= 0 || distance > _settings.MaximumWalkingDistanceMeters || duration <= 0)
                    throw new FormatException("Invalid route distance or duration.");

                var instructions = new List<RouteInstruction>();
                if (properties.TryGetProperty("segments", out var segments))
                    foreach (var segment in segments.EnumerateArray())
                        if (segment.TryGetProperty("steps", out var steps))
                            foreach (var step in steps.EnumerateArray())
                            {
                                var waypoints = step.GetProperty("way_points");
                                var start = waypoints[0].GetInt32();
                                var end = waypoints[1].GetInt32();
                                var stepDistance = step.GetProperty("distance").GetDouble();
                                var stepDuration = step.GetProperty("duration").GetDouble();
                                if (start < 0 || end < start || end >= points.Length || !double.IsFinite(stepDistance) || !double.IsFinite(stepDuration) || stepDistance < 0 || stepDuration < 0)
                                    throw new FormatException("Invalid navigation instruction.");

                                instructions.Add(new(step.GetProperty("instruction").GetString() ?? string.Empty, stepDistance, stepDuration, start, end));
                            }

                routes.Add(new($"provider-{index}", line, distance, duration, conventional && index == 0, "OpenRouteService", attribution, instructions));
            }
            catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or ArgumentException)
            {
                if (conventional && index == 0)
                    throw new FormatException("The conventional route is invalid.");
            }

            index++;
        }

        return routes;
    }
}
