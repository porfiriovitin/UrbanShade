using UrbanShade.Domain.Enums;
using UrbanShade.Domain.RegionThermalAnalysis;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;
using UrbanShade.Communication.Requests.RegionThermalAnalyses;

namespace UseCases.Tests.RegionThermalAnalyses;

public sealed class ExposureIndexCalculatorTests
{
    private readonly ExposureIndexCalculator _calculator = new(new RegionThermalAnalysisSettings());

    [Theory]
    [InlineData(25, -2, 100, 0, ExposureLevel.LOW)]
    [InlineData(30, 0.5, 75, 25, ExposureLevel.MODERATE)]
    [InlineData(35, 3, 50, 50, ExposureLevel.HIGH)]
    [InlineData(40, 5.5, 25, 75, ExposureLevel.CRITICAL)]
    [InlineData(60, 20, 0, 100, ExposureLevel.CRITICAL)]
    [InlineData(10, -10, 100, 0, ExposureLevel.LOW)]
    public void NormalizesWeightsAndClassifies(decimal heatIndex, decimal anomaly, decimal vegetation, decimal expected, ExposureLevel level)
    {
        var result = _calculator.Calculate(new(heatIndex, anomaly, vegetation, 3, 3, false, 95));
        Assert.Equal(expected, result.Index);
        Assert.Equal(level, result.Level);
        Assert.Equal(RegionDataQuality.COMPLETE, result.Quality);
        Assert.Equal(1, result.AppliedWeights.Values.Sum());
    }

    [Fact]
    public void MissingSurface_RedistributesWeights_WithoutReplacingMissingDataWithZero()
    {
        var result = _calculator.Calculate(new(35, null, 20, 2, 2, true, 95));
        Assert.Equal(58.57m, result.Index);
        Assert.Equal(RegionDataQuality.PARTIAL, result.Quality);
        Assert.False(result.Scores.ContainsKey("SurfaceTemperature"));
        Assert.Null(result.IsHeatIslandCandidate);
    }

    [Theory]
    [InlineData(null, 4, 20)]
    [InlineData(35, null, null)]
    [InlineData(null, null, null)]
    public void MissingMinimumData_DoesNotInventIndex(int? thermal, int? anomaly, int? vegetation)
    {
        var result = _calculator.Calculate(new(thermal, anomaly, vegetation, 0, 0, false, null));
        Assert.Null(result.Index);
        Assert.Null(result.Level);
        Assert.Empty(result.AppliedWeights);
        Assert.Equal(RegionDataQuality.INSUFFICIENT_DATA, result.Quality);
        Assert.Equal(ConfidenceLevel.LOW, result.Confidence);
    }

    [Theory]
    [InlineData(3, 29.99, true)]
    [InlineData(3, 30, false)]
    [InlineData(2.99, 10, false)]
    public void HeatIsland_UsesInclusiveAnomalyAndExclusiveVegetationThreshold(decimal anomaly, decimal vegetation, bool candidate)
    {
        Assert.Equal(candidate, _calculator.Calculate(new(35, anomaly, vegetation, 3, 3, false, 95)).IsHeatIslandCandidate);
    }

    [Theory]
    [InlineData(3, 3, false, 90, ConfidenceLevel.HIGH)]
    [InlineData(3, 3, true, 95, ConfidenceLevel.MEDIUM)]
    [InlineData(3, 2, false, 95, ConfidenceLevel.MEDIUM)]
    [InlineData(3, 3, false, 89.99, ConfidenceLevel.MEDIUM)]
    [InlineData(2, 3, false, 95, ConfidenceLevel.MEDIUM)]
    [InlineData(1, 1, false, 95, ConfidenceLevel.LOW)]
    [InlineData(0, 0, false, 95, ConfidenceLevel.MEDIUM)]
    public void HighConfidence_RequiresCompleteRealAndSpatiallyDistributedObservations(int sensors, int quadrants, bool simulated, decimal pixels, ConfidenceLevel expected)
    {
        Assert.Equal(expected, _calculator.Calculate(new(35, 3, 20, sensors, quadrants, simulated, pixels)).Confidence);
    }

    [Fact]
    public void PartialData_CannotReceiveHighConfidence()
    {
        Assert.Equal(ConfidenceLevel.MEDIUM, _calculator.Calculate(new(35, null, 20, 4, 4, false, 100)).Confidence);
    }

    [Fact]
    public void ZeroWeightComponent_DoesNotCauseDivisionOrLookupErrors()
    {
        var settings = new RegionThermalAnalysisSettings();
        settings.Weights.SurfaceTemperature = 0;
        settings.Weights.Thermal = 0.8m;
        settings.Validate();
        Assert.Equal(56, new ExposureIndexCalculator(settings).Calculate(new(35, 3, 20, 3, 3, false, 95)).Index);
    }

    [Fact]
    public void Settings_RejectInvalidWeightsThresholdsAndHighConfidenceRules()
    {
        var settings = new RegionThermalAnalysisSettings();
        settings.Weights.Thermal = -1;
        Assert.Throws<InvalidOperationException>(settings.Validate);
        settings = new();
        settings.Confidence.MinimumOccupiedQuadrants = 2;
        Assert.Throws<InvalidOperationException>(settings.Validate);
        settings = new();
        settings.RiskThresholds.High = 10;
        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Theory]
    [InlineData("NaN,0,1,1")]
    [InlineData("-181,0,1,1")]
    [InlineData("1,0,0,1")]
    [InlineData("0,0,1,91")]
    public void Queries_RejectInvalidBounds(string bbox)
    {
        Assert.False(new RegionThermalQueryValidator().Validate(new RegionThermalQueryRequest { Bbox = bbox }).IsValid);
    }
}
