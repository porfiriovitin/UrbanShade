using UrbanShade.Domain.Entities;

namespace UrbanShade.Domain.RegionThermalAnalysis;

public sealed record RegionSensorData(Sensor Sensor, SensorReading Reading, Entities.ThermalAnalysis Analysis, bool IsSimulated);

public sealed record RegionAnalysisData(GeoRegion Region, IReadOnlyList<RegionSensorData> Sensors,
    WeatherSnapshot? Weather, SatelliteAnalysis? Vegetation, SatelliteAnalysis? Surface,
    GeoRegion? ReferenceRegion, SatelliteAnalysis? ReferenceSurface);

public sealed record ExposureInput(decimal? HeatIndex, decimal? SurfaceAnomaly, decimal? VegetationCoveragePercent,
    int SensorCount, int OccupiedQuadrants, bool HasSimulatedSensors, decimal? MinimumValidPixelPercent);
