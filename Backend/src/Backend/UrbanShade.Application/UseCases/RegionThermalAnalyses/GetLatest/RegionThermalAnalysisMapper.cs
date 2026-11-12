using System.Text.Json;
using UrbanShade.Communication.Responses.RegionThermalAnalyses;
using UrbanShade.Domain.Entities;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;

public static class RegionThermalAnalysisMapper
{
    public static RegionThermalAnalysisResponse ToResponse(RegionThermalAnalysis analysis, string name) => new()
    {
        Id = analysis.Id, RegionId = analysis.GeoRegionId, RegionName = name, BoundaryVersion = analysis.BoundaryVersion,
        ExposureIndex = analysis.ExposureIndex, ExposureLevel = analysis.ExposureLevel?.ToString(), AverageHeatIndex = analysis.AverageHeatIndex,
        SurfaceTemperature = analysis.SurfaceTemperature, SurfaceTemperatureAnomaly = analysis.SurfaceTemperatureAnomaly,
        MeanNdvi = analysis.MeanNdvi, VegetationCoveragePercent = analysis.VegetationCoveragePercent, SensorCount = analysis.SensorCount,
        IsHeatIslandCandidate = analysis.IsHeatIslandCandidate, DataQuality = analysis.DataQuality.ToString(), ConfidenceLevel = analysis.ConfidenceLevel.ToString(),
        DataOrigin = analysis.DataOrigin, AnalysisMode = analysis.DataOrigin == "SIMULATED_SENSORS" ? "DEMONSTRATION" : "EXPERIMENTAL",
        AlgorithmVersion = analysis.AlgorithmVersion, AnalyzedAt = analysis.AnalyzedAt,
        SourceMetadata = UrbanShade.Application.UseCases.InstitutionalDashboard.InstitutionalDashboardQuery.PublicMetadata(analysis.SourceMetadata)
    };
}
