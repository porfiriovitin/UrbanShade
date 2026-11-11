using System.Text.Json;
using UrbanShade.Application.UseCases.GeoRegions;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Exceptions;
using UrbanShade.Infraestructure.Geospatial;

namespace UseCases.Tests.Geospatial;

public sealed class GeoExpressionTests
{
    public static JsonElement Boundary => JsonSerializer.SerializeToElement(new
    {
        type = "Polygon",
        coordinates = new[] { new[] {
        new[] { -46.657, -23.589 }, new[] { -46.654, -23.589 }, new[] { -46.654, -23.586 }, new[] { -46.657, -23.586 }, new[] { -46.657, -23.589 } } }
    });
    [Fact]
    public void GeoJson_RoundTripsHolesAndMultiPolygons_PreservesLongitudeLatitude()
    {
        var geometry = GeoJsonGeometry.Read(Boundary);
        Assert.Equal(4326, geometry.SRID);
        Assert.Equal(-46.657, geometry.Coordinate.X);
        Assert.Equal(-23.589, geometry.Coordinate.Y);
        Assert.True(geometry.EqualsExact(GeoJsonGeometry.Read(GeoJsonGeometry.Write(geometry))));
        var multi = JsonSerializer.SerializeToElement(new { type = "MultiPolygon", coordinates = new[] { Boundary.GetProperty("coordinates") } });
        Assert.Equal("MultiPolygon", GeoJsonGeometry.Read(multi).GeometryType);
        var hole = JsonDocument.Parse("{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[1,0],[1,1],[0,1],[0,0]],[[0.2,0.2],[0.2,0.4],[0.4,0.4],[0.4,0.2],[0.2,0.2]]]}").RootElement;
        Assert.True(GeoJsonGeometry.Read(hole).EqualsExact(GeoJsonGeometry.Read(GeoJsonGeometry.Write(GeoJsonGeometry.Read(hole)))));
    }
    [Theory]
    [InlineData("{\"type\":\"Point\",\"coordinates\":[0,0]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[1,1],[0,1],[1,0],[0,0]]]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[181,0],[181,1],[182,1],[181,0]]]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[1,0],[1,1]]]}")]
    public void GeometryValidator_RejectsInvalidGeometry(string json) => Assert.Throws<GeoException>(() => GeoRegionValidator.ReadBoundary(JsonDocument.Parse(json).RootElement, new GeoAnalysisSettings()));
    [Fact]
    public void Region_ChangesBoundaryVersionOnlyWhenGeometryChanges_AndPreservesHistoryOnDeactivate()
    {
        var region = new GeoRegion(Guid.NewGuid(), "Park", null, GeoJsonGeometry.Read(Boundary));
        Assert.Equal(1, region.BoundaryVersion);
        region.Update("Renamed", null, GeoJsonGeometry.Read(Boundary));
        Assert.Equal(1, region.BoundaryVersion);
        var shifted = GeoJsonGeometry.Read(Boundary);
        shifted.Coordinates[0].X -= 0.001;
        shifted.Coordinates[^1].X -= 0.001;
        shifted.GeometryChanged();
        region.Update("Renamed", null, shifted);
        Assert.Equal(2, region.BoundaryVersion);
        region.Deactivate();
        Assert.False(region.Active);
        Assert.False(region.Boundary.IsEmpty);
    }
    [Theory]
    [InlineData(GeoAnalysisType.Vegetation)]
    [InlineData(GeoAnalysisType.SurfaceTemperature)]
    public void RestGraph_AllReferencesResolve_AndResultIsBounded(GeoAnalysisType type)
    {
        var graph = Graph(type);
        var values = graph.GetProperty("values");
        Assert.True(values.TryGetProperty(graph.GetProperty("result").GetString()!, out _));
        Visit(graph);
        void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == "valueReference") Assert.True(values.TryGetProperty(property.Value.GetString()!, out _));
                    else Visit(property.Value);
                }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) Visit(child);
        }
        var observation = JsonSerializer.SerializeToElement(new EarthEngineExpressionBuilder().Observation(GeoJsonGeometry.Read(Boundary), "test", type, new GeoAnalysisSettings().Parameters(), new GoogleEarthEngineOptions()));
        Assert.Contains("maxPixels", observation.GetRawText());
        Assert.Contains("EPSG:4326", graph.GetRawText());
        Assert.DoesNotContain("private_key", graph.GetRawText());
        if (type == GeoAnalysisType.Vegetation)
        {
            Assert.Contains("Join.saveFirst", graph.GetRawText());
            Assert.Contains("cloud_probability", graph.GetRawText());
        }
        else { Assert.Contains("L2SP", graph.GetRawText()); Assert.Contains("QA_RADSAT", observation.GetRawText()); }
    }
    [Theory]
    [InlineData(GeoAnalysisType.Vegetation, 0, 0, 0.6)]
    [InlineData(GeoAnalysisType.SurfaceTemperature, 0, 0, 12.5708)]
    public void ActualGraph_EvaluatesNdviAndTemperature(GeoAnalysisType type, int qa, int saturated, double expected)
    {
        var value = EvaluateValue(type, qa, saturated, 4, 0);
        Assert.True(value.Valid);
        Assert.Equal(expected, value.Value, 5);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void ActualGraph_MasksLandsatQualityBits(int bit) => Assert.False(EvaluateValue(GeoAnalysisType.SurfaceTemperature, bit, 0, 4, 0).Valid);
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void ActualGraph_MasksSentinelSceneClasses(int scl) => Assert.False(EvaluateValue(GeoAnalysisType.Vegetation, 0, 0, scl, 0).Valid);
    [Fact]
    public void ActualGraph_MasksSaturationAndCloudProbability()
    {
        Assert.False(EvaluateValue(GeoAnalysisType.SurfaceTemperature, 0, 1, 4, 0).Valid);
        Assert.False(EvaluateValue(GeoAnalysisType.Vegetation, 0, 0, 4, 41).Valid);
    }
    private static JsonElement Graph(GeoAnalysisType type) => JsonSerializer.SerializeToElement(new EarthEngineExpressionBuilder().Candidates(GeoJsonGeometry.Read(Boundary),
        new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), type, new GeoAnalysisSettings().Parameters(), new GoogleEarthEngineOptions()));
    private sealed record Pixel(double Value, bool Valid = true);
    private static Pixel EvaluateValue(GeoAnalysisType type, int qa, int saturated, int scl, int cloud)
    {
        var values = JsonSerializer.SerializeToElement(new EarthEngineExpressionBuilder().Observation(GeoJsonGeometry.Read(Boundary), "test", type, new GeoAnalysisSettings().Parameters(), new GoogleEarthEngineOptions())).GetProperty("values");
        var rename = values.EnumerateObject().First(property => property.Value.TryGetProperty("functionInvocationValue", out var call) && call.GetProperty("functionName").GetString() == "Image.rename").Value;
        return Evaluate(rename);
        Pixel Evaluate(JsonElement node)
        {
            if (node.TryGetProperty("valueReference", out var reference)) return Evaluate(values.GetProperty(reference.GetString()!));
            if (node.TryGetProperty("constantValue", out var constant)) return new(constant.GetDouble());
            var call = node.GetProperty("functionInvocationValue");
            var args = call.GetProperty("arguments");
            var name = call.GetProperty("functionName").GetString();
            if (name == "Image.select")
            {
                var band = values.GetProperty(args.GetProperty("bandSelectors").GetProperty("valueReference").GetString()!).GetProperty("constantValue")[0].GetString();
                return new(band switch { "B8" => 8000, "B4" => 2000, "ST_B10" => 40000, "QA_PIXEL" => qa, "QA_RADSAT" => saturated, "SCL" => scl, "probability" => cloud, _ => throw new InvalidOperationException() });
            }
            if (name == "Image.constant") return Evaluate(args.GetProperty("value"));
            if (name == "Image.rename") return Evaluate(args.GetProperty("input"));
            if (name == "Image.updateMask") { var image = Evaluate(args.GetProperty("image")); var mask = Evaluate(args.GetProperty("mask")); return image with { Valid = image.Valid && mask.Valid && mask.Value != 0 }; }
            var left = Evaluate(args.GetProperty("image1"));
            var right = Evaluate(args.GetProperty("image2"));
            var value = name switch
            {
                "Image.add" => left.Value + right.Value,
                "Image.subtract" => left.Value - right.Value,
                "Image.multiply" => left.Value * right.Value,
                "Image.divide" => left.Value / right.Value,
                "Image.bitwiseAnd" => (int)left.Value & (int)right.Value,
                "Image.and" => left.Value != 0 && right.Value != 0 ? 1 : 0,
                "Image.eq" => left.Value == right.Value ? 1 : 0,
                "Image.neq" => left.Value != right.Value ? 1 : 0,
                "Image.gt" => left.Value > right.Value ? 1 : 0,
                "Image.lte" => left.Value <= right.Value ? 1 : 0,
                _ => throw new InvalidOperationException(name)
            };
            return new(value, left.Valid && right.Valid);
        }
    }
}
