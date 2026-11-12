using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;

namespace UrbanShade.Domain.Entities;

public class SatelliteAnalysis : EntityBase
{
    private SatelliteAnalysis()
    {
    }

    public SatelliteAnalysis(GeoAnalysisJob job, GeoAnalysisType type, SatelliteObservation observation)
    {
        GeoRegionId = job.GeoRegionId;
        GeoAnalysisJobId = job.Id;
        BoundaryVersion = job.BoundaryVersion;
        AnalysisType = type;
        DataSource = observation.Source;
        MeanValue = observation.Mean;
        MinValue = observation.Minimum;
        MaxValue = observation.Maximum;
        VegetationCoveragePercent = observation.VegetationCoveragePercent;
        VegetationAreaSquareMeters = observation.VegetationAreaSquareMeters;
        ValidPixelPercent = observation.ValidPixelPercent;
        ObservationStartAt = ObservationEndAt = observation.ObservedAt;
        AlgorithmVersion = job.AlgorithmVersion;
        DatasetId = observation.DatasetId;
        ImageId = observation.ImageId;
        Metadata = observation.Metadata;
    }

    public Guid GeoRegionId { get; private set; }
    public Guid GeoAnalysisJobId { get; private set; }
    public int BoundaryVersion { get; private set; }
    public SatelliteDataSource DataSource { get; private set; }
    public GeoAnalysisType AnalysisType { get; private set; }
    public decimal? MeanValue { get; private set; }
    public decimal? MinValue { get; private set; }
    public decimal? MaxValue { get; private set; }
    public decimal? VegetationCoveragePercent { get; private set; }
    public decimal? VegetationAreaSquareMeters { get; private set; }
    public decimal ValidPixelPercent { get; private set; }
    public DateTime ObservationStartAt { get; private set; }
    public DateTime ObservationEndAt { get; private set; }
    public DateTime ProcessedAt { get; private set; } = DateTime.UtcNow;
    public string AlgorithmVersion { get; private set; } = string.Empty;
    public string DatasetId { get; private set; } = string.Empty;
    public string ImageId { get; private set; } = string.Empty;
    public string Metadata { get; private set; } = "{}";
}
