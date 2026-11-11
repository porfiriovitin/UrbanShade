using UrbanShade.Application.UseCases.SensorReadings.GetHistory;
using UrbanShade.Communication.Requests.SensorReadings;
using UrbanShade.Domain.Simulation;

namespace Validator.Tests;

public sealed class SensorReadingValidatorTests
{
    [Theory]
    [InlineData(1, 20, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(-1, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    [InlineData(int.MaxValue, 100, false)]
    public void History_ValidatesPagination(int page, int pageSize, bool valid)
    {
        Assert.Equal(valid, new GetSensorReadingsValidator().Validate(new GetSensorReadingsRequest { Page = page, PageSize = pageSize }).IsValid);
    }

    [Fact]
    public void History_ValidatesDateOrderUsingOffsets()
    {
        var start = DateTimeOffset.Parse("2026-11-11T10:00:00-03:00");
        Assert.True(new GetSensorReadingsValidator().Validate(new GetSensorReadingsRequest { StartDate = start, EndDate = start.ToUniversalTime() }).IsValid);
        Assert.False(new GetSensorReadingsValidator().Validate(new GetSensorReadingsRequest { StartDate = start, EndDate = start.AddMinutes(-1) }).IsValid);
        Assert.True(new GetSensorReadingsValidator().Validate(new GetSensorReadingsRequest()).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1441)]
    [InlineData(0.000000001)]
    public void Settings_RejectsInvalidInterval(double interval)
    {
        Assert.Throws<InvalidOperationException>(() => new SensorSimulationSettings { IntervalMinutes = interval }.Validate());
    }

    [Fact]
    public void Settings_ValidatesRangesAndPrecision()
    {
        new SensorSimulationSettings().Validate();
        Assert.Throws<InvalidOperationException>(() => new SensorSimulationSettings { MinTemperature = 43 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new SensorSimulationSettings { MinHumidity = -1 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new SensorSimulationSettings { MaxHumidity = 101 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new SensorSimulationSettings { MinHumidity = 96 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new SensorSimulationSettings { MaxTemperatureVariation = -0.1m }.Validate());
        Assert.Throws<InvalidOperationException>(() => new SensorSimulationSettings { MaxHumidityVariation = -1 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new SensorSimulationSettings { MaxTemperatureVariation = 0.0000001m }.Validate());
        new SensorSimulationSettings { MinTemperature = 20, MaxTemperature = 20, MaxTemperatureVariation = 0, MaxHumidityVariation = 0 }.Validate();
    }
}
