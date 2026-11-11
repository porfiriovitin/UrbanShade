using System.Globalization;
using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Linemerge;
using NetTopologySuite.Simplify;
using UrbanShade.Domain.Geospatial;

namespace UrbanShade.Infraestructure.Geospatial;

public static class OverpassResponseParser
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);
    public static OpenStreetMapSnapshot Parse(JsonElement response, OpenStreetMapParameters parameters)
    {
        try
        {
            if (response.TryGetProperty("remark", out _))
                throw new OpenStreetMapException("OpenStreetMap returned an incomplete import. Previous data was preserved.");
            var reference = DateTime.Parse(response.GetProperty("osm3s").GetProperty("timestamp_osm_base").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            var elements = response.GetProperty("elements").EnumerateArray().ToArray();
            if (elements.Length > parameters.MaximumElements)
                throw new OpenStreetMapException("OpenStreetMap import exceeded the element limit. Use a smaller region.");
            var areas = new List<OpenStreetMapArea>();
            var relationWays = new HashSet<long>();
            foreach (var element in elements.Where(element => element.GetProperty("type").GetString() == "relation" && IsGreen(element)))
            {
                if (!element.GetProperty("tags").TryGetProperty("type", out var relationType) || relationType.GetString() != "multipolygon") continue;
                var members = element.GetProperty("members").EnumerateArray().Where(member => member.GetProperty("type").GetString() == "way").ToArray();
                var outer = Rings(members.Where(member => member.GetProperty("role").GetString() is "outer" or ""), parameters);
                var inner = Rings(members.Where(member => member.GetProperty("role").GetString() == "inner"), parameters);
                if (outer.Count == 0 || members.Any(member => member.GetProperty("role").GetString() is not ("outer" or "inner" or ""))) throw new ArgumentException();
                var shells = outer.Select(ring => Factory.CreatePolygon(ring)).ToArray();
                var holes = shells.Select(_ => new List<LinearRing>()).ToArray();
                foreach (var ring in inner)
                {
                    var polygon = Factory.CreatePolygon(ring);
                    var owner = Enumerable.Range(0, shells.Length).Where(index => shells[index].Covers(polygon)).OrderBy(index => shells[index].Area).FirstOrDefault(-1);
                    if (owner < 0) throw new ArgumentException();
                    holes[owner].Add(ring);
                }
                var polygons = outer.Select((shell, index) => Factory.CreatePolygon(shell, holes[index].ToArray())).ToArray();
                var geometry = Factory.CreateMultiPolygon(polygons);
                areas.Add(Area(element, geometry, parameters));
                foreach (var member in members) relationWays.Add(member.GetProperty("ref").GetInt64());
            }
            foreach (var element in elements.Where(element => element.GetProperty("type").GetString() == "way" && IsGreen(element)))
            {
                if (relationWays.Contains(element.GetProperty("id").GetInt64())) continue;
                var coordinates = Coordinates(element.GetProperty("geometry"), parameters);
                if (!coordinates[0].Equals2D(coordinates[^1])) continue;
                areas.Add(Area(element, Factory.CreatePolygon(coordinates), parameters));
            }
            return new(reference, areas.DistinctBy(area => (area.ElementType, area.ElementId)).ToArray());
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        {
            throw new OpenStreetMapException("OpenStreetMap returned invalid or unsupported geometry. Previous data was preserved.");
        }
    }
    private static bool IsGreen(JsonElement element)
    {
        if (!element.TryGetProperty("tags", out var tags)) return false;
        if (tags.TryGetProperty("area", out var area) && area.GetString() == "no") return false;
        return (tags.TryGetProperty("leisure", out var leisure) && leisure.GetString() is "park" or "garden") ||
            (tags.TryGetProperty("landuse", out var landuse) && landuse.GetString() is "forest" or "grass" or "village_green" or "recreation_ground") ||
            (tags.TryGetProperty("natural", out var natural) && natural.GetString() is "wood" or "grassland" or "scrub");
    }
    private static Coordinate[] Coordinates(JsonElement geometry, OpenStreetMapParameters parameters)
    {
        if (geometry.GetArrayLength() < 2 || geometry.GetArrayLength() > parameters.MaximumVertices) throw new ArgumentException();
        return geometry.EnumerateArray().Select(point =>
        {
            var longitude = point.GetProperty("lon").GetDouble();
            var latitude = point.GetProperty("lat").GetDouble();
            if (!double.IsFinite(longitude) || !double.IsFinite(latitude) || longitude is < -180 or > 180 || latitude is < -90 or > 90) throw new ArgumentException();
            return new Coordinate(longitude, latitude);
        }).ToArray();
    }
    private static List<LinearRing> Rings(IEnumerable<JsonElement> members, OpenStreetMapParameters parameters)
    {
        var merger = new LineMerger();
        var count = 0;
        foreach (var member in members)
        {
            var coordinates = Coordinates(member.GetProperty("geometry"), parameters);
            count += coordinates.Length;
            if (count > parameters.MaximumVertices) throw new ArgumentException();
            merger.Add(Factory.CreateLineString(coordinates));
        }
        return merger.GetMergedLineStrings().Cast<LineString>().Select(line =>
        {
            if (!line.IsClosed || line.NumPoints < 4) throw new ArgumentException();
            return Factory.CreateLinearRing(line.Coordinates);
        }).ToList();
    }
    private static OpenStreetMapArea Area(JsonElement element, Geometry geometry, OpenStreetMapParameters parameters)
    {
        if (geometry.IsEmpty || !geometry.IsValid || geometry.NumPoints > parameters.MaximumVertices) throw new ArgumentException();
        /// :: Bound map response complexity without replacing malformed polygons with invented boundaries.
        var tolerance = 0.00001;
        while (geometry.NumPoints > 2000 && tolerance <= 0.001)
        {
            geometry = TopologyPreservingSimplifier.Simplify(geometry, tolerance);
            tolerance *= 2;
        }
        if (geometry.NumPoints > 2000 || geometry.EnvelopeInternal.Width > 180) throw new ArgumentException();
        var tags = element.GetProperty("tags");
        if (tags.GetRawText().Length > 16000) throw new ArgumentException();
        var name = tags.TryGetProperty("name", out var label) ? label.GetString() : null;
        if (name?.Length > 500) name = name[..500];
        var id = element.GetProperty("id").GetInt64();
        if (id <= 0) throw new ArgumentException();
        return new(element.GetProperty("type").GetString()!, id, name, geometry, tags.GetRawText());
    }
}
