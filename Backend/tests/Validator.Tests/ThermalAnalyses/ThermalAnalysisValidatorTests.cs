using UrbanShade.Application.UseCases.ThermalAnalyses.GetHistory;
using UrbanShade.Communication.Requests.ThermalAnalyses;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.ThermalAnalysis;

namespace Validator.Tests.ThermalAnalyses;

public sealed class ThermalAnalysisValidatorTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("LOW", true)]
    [InlineData("moderate", true)]
    [InlineData("HIGH", true)]
    [InlineData("CRITICAL", true)]
    [InlineData("0", false)]
    [InlineData("9", false)]
    [InlineData("UNKNOWN", false)]
    [InlineData("LOW, HIGH", false)]
    [InlineData("", false)]
    public void History_ValidatesRiskNames(string? risk, bool valid)
    {
        Assert.Equal(valid, new GetThermalAnalysesValidator().Validate(new GetThermalAnalysesRequest { RiskLevel = risk }).IsValid);
    }

    [Fact]
    public void History_ValidatesPaginationDatesAndVersion()
    {
        var validator = new GetThermalAnalysesValidator();
        Assert.True(validator.Validate(new GetThermalAnalysesRequest()).IsValid);
        Assert.False(validator.Validate(new GetThermalAnalysesRequest { Page = 0 }).IsValid);
        Assert.False(validator.Validate(new GetThermalAnalysesRequest { PageSize = 101 }).IsValid);
        Assert.False(validator.Validate(new GetThermalAnalysesRequest { Page = int.MaxValue }).IsValid);
        Assert.False(validator.Validate(new GetThermalAnalysesRequest { AlgorithmVersion = " " }).IsValid);
        Assert.False(validator.Validate(new GetThermalAnalysesRequest { AlgorithmVersion = new string('a', 33) }).IsValid);
        var start = DateTimeOffset.Parse("2026-11-11T10:00:00-03:00");
        Assert.True(validator.Validate(new GetThermalAnalysesRequest { StartDate = start, EndDate = start.ToUniversalTime() }).IsValid);
        Assert.False(validator.Validate(new GetThermalAnalysesRequest { StartDate = start, EndDate = start.AddSeconds(-1) }).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(525601)]
    public void Settings_RejectsInvalidStaleness(double minutes)
    {
        Assert.Throws<InvalidOperationException>(() => new ThermalAnalysisSettings { StaleDataThresholdMinutes = minutes }.Validate());
    }

    [Fact]
    public void Settings_ValidatesVersionAndOrderedCelsiusThresholds()
    {
        new ThermalAnalysisSettings().Validate();
        Assert.Throws<InvalidOperationException>(() => new ThermalAnalysisSettings { AlgorithmVersion = "" }.Validate());
        Assert.Throws<InvalidOperationException>(() => new ThermalAnalysisSettings { AlgorithmVersion = " 1.0 " }.Validate());
        Assert.Throws<InvalidOperationException>(() => new ThermalAnalysisSettings { RiskThresholds = new() { Moderate = 32, High = 32 } }.Validate());
        Assert.Throws<InvalidOperationException>(() => new ThermalAnalysisSettings { RiskThresholds = new() { High = 39, Critical = 38 } }.Validate());
        var settings = new ThermalAnalysisSettings { AlgorithmVersion = "2.0", RiskThresholds = new() { Moderate = 28, High = 33, Critical = 40 } };
        settings.Validate();
        Assert.Equal(ThermalRiskLevel.LOW, settings.Classify(27));
        Assert.Equal(ThermalRiskLevel.MODERATE, settings.Classify(28));
        Assert.Equal(ThermalRiskLevel.HIGH, settings.Classify(33));
        Assert.Equal(ThermalRiskLevel.CRITICAL, settings.Classify(40));
    }
}
