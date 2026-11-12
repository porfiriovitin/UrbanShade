using NetTopologySuite.Geometries;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Entities;

public class GreenArea : EntityBase
{
    private GreenArea()
    {
    }

    public GreenArea(Guid regionId, Guid analysisId, Geometry geometry, decimal area, DateTime referenceDate)
    {
        GeoRegionId = regionId;
        SatelliteAnalysisId = analysisId;
        Geometry = geometry;
        EstimatedAreaSquareMeters = area;
        ReferenceDate = referenceDate;
    }

    public string? OsmElementType { get; private set; }
    public long? OsmElementId { get; private set; }
    public DateTime? ImportedAt { get; private set; }
    public string SourceMetadata { get; private set; } = "{}";
    public string? Name { get; private set; }
    public Geometry Geometry { get; private set; } = null!;
    public GreenAreaSource Source { get; private set; }
    public decimal EstimatedAreaSquareMeters { get; private set; }
    public DateTime? ReferenceDate { get; private set; }
    public Guid? GeoRegionId { get; private set; }
    public Guid? SatelliteAnalysisId { get; private set; }
}
