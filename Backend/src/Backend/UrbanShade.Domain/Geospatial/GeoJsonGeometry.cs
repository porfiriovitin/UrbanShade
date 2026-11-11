using System.Text.Json;
using NetTopologySuite.Geometries;

namespace UrbanShade.Domain.Geospatial;

public static class GeoJsonGeometry
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);
    public static Geometry Read(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object) throw new ArgumentException("A GeoJSON polygon is required.");
        var type = json.GetProperty("type").GetString();
        var coordinates = json.GetProperty("coordinates");
        return type switch
        {
            "Polygon" => Polygon(coordinates),
            "MultiPolygon" => Factory.CreateMultiPolygon(coordinates.EnumerateArray().Select(Polygon).ToArray()),
            _ => throw new ArgumentException("Only Polygon and MultiPolygon geometries are supported.")
        };
    }
    private static Polygon Polygon(JsonElement coordinates)
    {
        var rings = coordinates.EnumerateArray().Select(ring => Factory.CreateLinearRing(ring.EnumerateArray().Select(point =>
        {
            if (point.GetArrayLength() != 2) throw new ArgumentException("Coordinates must contain longitude and latitude.");
            var x = point[0].GetDouble();
            var y = point[1].GetDouble();
            if (!double.IsFinite(x) || !double.IsFinite(y) || x is < -180 or > 180 || y is < -90 or > 90)
                throw new ArgumentException("Coordinates are outside geographic bounds.");
            return new Coordinate(x, y);
        }).ToArray())).ToArray();
        if (rings.Length == 0) throw new ArgumentException("A polygon must contain at least one ring.");
        return Factory.CreatePolygon(rings[0], rings.Skip(1).ToArray());
    }
    public static JsonElement Write(Geometry geometry)
    {
        object Ring(LineString ring) => ring.Coordinates.Select(point => new[] { point.X, point.Y }).ToArray();
        object Coordinates(Polygon polygon) => new[] { Ring(polygon.ExteriorRing) }.Concat(Enumerable.Range(0, polygon.NumInteriorRings).Select(i => Ring(polygon.GetInteriorRingN(i)))).ToArray();
        return geometry switch
        {
            Polygon polygon => JsonSerializer.SerializeToElement(new { type = "Polygon", coordinates = Coordinates(polygon) }),
            MultiPolygon polygons => JsonSerializer.SerializeToElement(new { type = "MultiPolygon", coordinates = Enumerable.Range(0, polygons.NumGeometries).Select(i => Coordinates((Polygon)polygons.GetGeometryN(i))).ToArray() }),
            _ => throw new ArgumentException("Only polygon geometries are supported.")
        };
    }
}
