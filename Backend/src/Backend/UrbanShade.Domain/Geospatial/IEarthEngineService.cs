using NetTopologySuite.Geometries;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Geospatial;

public interface IEarthEngineService
{
    Task<SatelliteObservation?> Analyze(Geometry boundary, DateTime start, DateTime end, GeoAnalysisType type, GeoProcessingParameters parameters, CancellationToken cancellationToken);
}

public sealed record GeoProcessingParameters(double VegetationNdviThreshold, double MinimumValidPixelPercent, int MaxCloudProbability, double MinimumGreenAreaSquareMeters, int MaximumGreenAreas);
public sealed record SatelliteObservation(SatelliteDataSource Source, string DatasetId, string ImageId, DateTime ObservedAt, decimal Mean, decimal Minimum, decimal Maximum, decimal ValidPixelPercent, decimal? VegetationCoveragePercent, decimal? VegetationAreaSquareMeters, string Metadata, IReadOnlyList<VegetationPolygon> GreenAreas);
public sealed record VegetationPolygon(Geometry Geometry, decimal AreaSquareMeters);
public sealed class EarthEngineException : Exception
{
    public EarthEngineException(string message, bool transient = false) : base(message)
    {
        Transient = transient;
    }

    public bool Transient { get; }
}
