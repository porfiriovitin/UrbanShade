using System.Text.Json;
using NetTopologySuite.Geometries;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;

namespace UrbanShade.Infraestructure.Geospatial;

public sealed class EarthEngineExpressionBuilder
{
    private readonly Dictionary<string, object> _values = [];
    private object Node(object value)
    {
        var id = "n" + _values.Count;
        _values.Add(id, value);
        return new { valueReference = id };
    }
    private object Constant(object? value) => Node(new { constantValue = value });
    private object Call(string name, params (string Key, object Value)[] arguments) =>
        Node(new { functionInvocationValue = new { functionName = name, arguments = arguments.ToDictionary(argument => argument.Key, argument => argument.Value) } });
    private object Dictionary(params (string Key, object Value)[] items) => Call("Dictionary.fromLists", ("keys", Constant(items.Select(item => item.Key).ToArray())),
        ("values", Node(new { arrayValue = new { values = items.Select(item => item.Value).ToArray() } })));
    private object Get(object dictionary, string key, object? defaultValue = null) => Call("Dictionary.get", ("dictionary", dictionary), ("key", Constant(key)), ("defaultValue", Constant(defaultValue)));
    private object Property(object element, string key) => Call("Element.get", ("object", element), ("property", Constant(key)));
    private object Select(object image, string band) => Call("Image.select", ("input", image), ("bandSelectors", Constant(new[] { band })));
    private object ImageNumber(double number) => Call("Image.constant", ("value", Constant(number)));
    private object Binary(string operation, object left, object right) => Call("Image." + operation, ("image1", left), ("image2", right));
    private object And(object left, object right) => Binary("and", left, right);
    private object Mask(object image, object mask) => Call("Image.updateMask", ("image", image), ("mask", mask));
    private object Geometry(Geometry boundary)
    {
        var geoJson = GeoJsonGeometry.Write(boundary);
        return Call("GeometryConstructors." + geoJson.GetProperty("type").GetString(), ("coordinates", Constant(geoJson.GetProperty("coordinates"))),
            ("crs", Call("Projection", ("crs", Constant("EPSG:4326")))), ("geodesic", Constant(true)));
    }
    private object Filter(object collection, object filter) => Call("Collection.filter", ("collection", collection), ("filter", filter));
    private object Load(string id) => Call("ImageCollection.load", ("id", Constant(id)));
    private object Reduce(object image, object reducer, object geometry, int scale) => Call("Image.reduceRegion", ("image", image), ("reducer", reducer),
        ("geometry", geometry), ("scale", Constant(scale)), ("crs", Call("Projection", ("crs", Constant("EPSG:4326")))), ("maxPixels", Constant(20_000_000)), ("tileScale", Constant(4)));
    private object SumArea(object mask, object geometry, int scale)
    {
        var area = Call("Image.pixelArea");
        return Get(Reduce(Mask(area, mask), Call("Reducer.sum"), geometry, scale), "area", 0);
    }
    private object Function(string argumentName, object body) => Node(new { functionDefinitionValue = new { argumentNames = new[] { argumentName }, body = JsonSerializer.SerializeToElement(body).GetProperty("valueReference").GetString() } });
    private object Argument(string name) => Node(new { argumentReference = name });
    private object Rename(object image) => Call("Image.rename", ("input", image), ("names", Constant(new[] { "value" })));
    private object BuildValue(object image, bool vegetation, GeoProcessingParameters parameters)
    {
        if (vegetation)
        {
            var scl = Select(image, "SCL");
            object quality = Binary("neq", scl, ImageNumber(0));
            foreach (var excluded in new[] { 1, 3, 8, 9, 10, 11 }) quality = And(quality, Binary("neq", scl, ImageNumber(excluded)));
            var cloud = Select(Property(image, "cloud_probability"), "probability");
            quality = And(quality, Binary("lte", cloud, ImageNumber(parameters.MaxCloudProbability)));
            var nir = Select(image, "B8");
            var red = Select(image, "B4");
            var denominator = Binary("add", nir, red);
            var ndvi = Binary("divide", Binary("subtract", nir, red), denominator);
            return Rename(Mask(ndvi, And(quality, Binary("neq", denominator, ImageNumber(0)))));
        }
        var qa = Select(image, "QA_PIXEL");
        /// :: Fill, dilated cloud, cirrus, cloud, shadow and snow bits must all be clear.
        var clear = Binary("eq", Binary("bitwiseAnd", qa, ImageNumber(63)), ImageNumber(0));
        clear = And(clear, Binary("eq", Select(image, "QA_RADSAT"), ImageNumber(0)));
        var thermal = Select(image, "ST_B10");
        clear = And(clear, Binary("gt", thermal, ImageNumber(0)));
        var celsius = Binary("subtract", Binary("add", Binary("multiply", thermal, ImageNumber(0.00341802)), ImageNumber(149)), ImageNumber(273.15));
        return Rename(Mask(celsius, clear));
    }
    public object Candidates(Geometry boundary, DateTime start, DateTime end, GeoAnalysisType type, GeoProcessingParameters parameters, GoogleEarthEngineOptions options)
    {
        var vegetation = type == GeoAnalysisType.Vegetation;
        var geometry = Geometry(boundary);
        var scale = vegetation ? 10 : 30;
        object collection = vegetation ? Load(options.SentinelDataset) : Call("ImageCollection.merge", ("collection1", Load(options.Landsat8Dataset)), ("collection2", Load(options.Landsat9Dataset)));
        var dateFilter = Call("Filter.dateRangeContains", ("leftValue", Call("DateRange", ("start", Call("Date", ("value", Constant(new DateTimeOffset(start).ToUnixTimeMilliseconds())))), ("end", Call("Date", ("value", Constant(new DateTimeOffset(end).ToUnixTimeMilliseconds())))))), ("rightField", Constant("system:time_start")));
        collection = Filter(collection, dateFilter);
        collection = Filter(collection, Call("Filter.intersects", ("leftField", Constant(".all")), ("rightValue", Call("Feature", ("geometry", geometry)))));
        if (!vegetation) collection = Filter(collection, Call("Filter.equals", ("leftField", Constant("PROCESSING_LEVEL")), ("rightValue", Constant("L2SP"))));
        collection = Call("Collection.limit", ("collection", collection), ("limit", Constant(20)), ("key", Constant("system:time_start")), ("ascending", Constant(false)));
        if (vegetation)
        {
            var cloudCollection = Filter(Filter(Load(options.CloudProbabilityDataset), dateFilter), Call("Filter.intersects", ("leftField", Constant(".all")), ("rightValue", Call("Feature", ("geometry", geometry)))));
            var join = Call("Join.saveFirst", ("matchKey", Constant("cloud_probability")), ("outer", Constant(false)));
            collection = Call("Join.apply", ("join", join), ("primary", collection), ("secondary", cloudCollection),
                ("condition", Call("Filter.equals", ("leftField", Constant("system:index")), ("rightField", Constant("system:index")))));
        }
        collection = Call("Collection.limit", ("collection", collection), ("limit", Constant(20)), ("key", Constant("system:time_start")), ("ascending", Constant(false)));
        return Expression(Dictionary(("imageIds", Call("AggregateFeatureCollection.array", ("collection", collection), ("property", Constant("system:id"))))));
    }
    public object Observation(Geometry boundary, string imageId, GeoAnalysisType type, GeoProcessingParameters parameters, GoogleEarthEngineOptions options)
    {
        var vegetation = type == GeoAnalysisType.Vegetation;
        var geometry = Geometry(boundary);
        var scale = vegetation ? 10 : 30;
        var image = Call("Image.load", ("id", Constant(imageId)));
        if (vegetation)
        {
            var cloudId = Constant(options.CloudProbabilityDataset + "/" + imageId.Split('/').Last());
            image = Call("Element.set", ("object", image), ("key", Constant("cloud_probability")), ("value", Call("Image.load", ("id", cloudId))));
        }
        var value = BuildValue(image, vegetation, parameters);
        var reducer = Call("Reducer.combine", ("reducer1", Call("Reducer.mean")), ("reducer2", Call("Reducer.minMax")), ("sharedInputs", Constant(true)));
        var stats = Reduce(value, reducer, geometry, scale);
        var validArea = SumArea(Call("Image.mask", ("image", value)), geometry, scale);
        var totalArea = Get(Reduce(Call("Image.pixelArea"), Call("Reducer.sum"), geometry, scale), "area", 0);
        var validPercent = Call("Number.multiply", ("left", Call("Number.divide", ("left", validArea), ("right", totalArea))), ("right", Constant(100)));
        validPercent = Call("If", ("condition", Call("Number.gt", ("left", totalArea), ("right", Constant(0)))), ("trueCase", validPercent), ("falseCase", Constant(0)));
        var vegetationArea = vegetation ? SumArea(Binary("gte", value, ImageNumber(parameters.VegetationNdviThreshold)), geometry, scale) : Constant(null);
        return Expression(Dictionary(("statistics", stats), ("imageId", Constant(imageId)), ("observedAt", Property(image, "system:time_start")),
            ("validPercent", validPercent), ("validArea", validArea), ("vegetationArea", vegetationArea)));
    }
    public object Vectors(Geometry boundary, string imageId, GeoProcessingParameters parameters, GoogleEarthEngineOptions options)
    {
        var image = Call("Image.load", ("id", Constant(imageId)));
        var cloudId = Constant(options.CloudProbabilityDataset + "/" + imageId.Split('/').Last());
        image = Call("Element.set", ("object", image), ("key", Constant("cloud_probability")), ("value", Call("Image.load", ("id", cloudId))));
        var value = BuildValue(image, true, parameters);
        var vegetation = Binary("gte", value, ImageNumber(parameters.VegetationNdviThreshold));
        vegetation = Mask(vegetation, vegetation);
        var vectors = Call("Image.reduceToVectors", ("image", vegetation), ("geometry", Geometry(boundary)), ("scale", Constant(10)), ("crs", Call("Projection", ("crs", Constant("EPSG:4326")))),
            ("geometryType", Constant("polygon")), ("eightConnected", Constant(false)), ("maxPixels", Constant(20_000_000)), ("tileScale", Constant(4)));
        var feature = Argument("feature");
        var area = Call("Geometry.area", ("geometry", Call("Feature.geometry", ("feature", feature))), ("maxError", Call("ErrorMargin", ("value", Constant(1)))));
        var withArea = Call("Element.set", ("object", feature), ("key", Constant("area")), ("value", area));
        vectors = Call("Collection.map", ("collection", vectors), ("baseAlgorithm", Function("feature", withArea)));
        vectors = Filter(vectors, Call("Filter.greaterThanOrEquals", ("leftField", Constant("area")), ("rightValue", Constant(parameters.MinimumGreenAreaSquareMeters))));
        vectors = Call("Collection.limit", ("collection", vectors), ("limit", Constant(parameters.MaximumGreenAreas)), ("key", Constant("area")), ("ascending", Constant(false)));
        return Expression(vectors);
    }
    private object Expression(object reference) => new { values = _values, result = JsonSerializer.SerializeToElement(reference).GetProperty("valueReference").GetString() };
}
