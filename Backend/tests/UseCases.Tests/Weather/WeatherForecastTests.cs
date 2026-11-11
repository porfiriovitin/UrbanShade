using UrbanShade.Application.UseCases.Weather;
using UrbanShade.Communication.Requests.Weather;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Weather;
using UrbanShade.Exceptions;

namespace UseCases.Tests.Weather;

public sealed class WeatherForecastTests
{
    [Fact]
    public void ForecastUpdate_PreservesAvailableFieldsForSameBoundary_AndIgnoresOlderRevisions()
    {
        var now = DateTime.UtcNow;
        var prediction = new WeatherPrediction(now.AddHours(1), 30, 60, 34, 15, 8, 20, 8, 780);
        var forecast = new WeatherForecast(Guid.NewGuid(), 1, -23, -46, prediction, now);
        forecast.Update(1, -23, -46, prediction with { Temperature = 32, Humidity = null, UvIndex = null }, now.AddMinutes(1));
        Assert.Equal(32, forecast.Temperature);
        Assert.Equal(60, forecast.Humidity);
        Assert.Equal(8, forecast.UvIndex);
        Assert.Equal(now.AddMinutes(1), forecast.FetchedAt);
        forecast.Update(1, -23, -46, prediction with { Temperature = 99 }, now);
        Assert.Equal(32, forecast.Temperature);
    }

    [Fact]
    public void BoundaryChange_DoesNotCarryMissingValuesFromPreviousLocation()
    {
        var now = DateTime.UtcNow;
        var prediction = new WeatherPrediction(now.AddHours(1), 30, 60, 34, 15, 8, 20, 8, 780);
        var forecast = new WeatherForecast(Guid.NewGuid(), 1, -23, -46, prediction, now);
        forecast.Update(2, -24, -47, prediction with { Temperature = 32, Humidity = null, UvIndex = null }, now.AddMinutes(1));
        Assert.Equal(2, forecast.BoundaryVersion);
        Assert.Equal(-24, forecast.Latitude);
        Assert.Equal(32, forecast.Temperature);
        Assert.Null(forecast.Humidity);
        Assert.Null(forecast.UvIndex);
    }

    [Fact]
    public void History_ValidatesUtcPeriodAndPaginationOverflow()
    {
        Assert.Throws<WeatherException>(() => WeatherValidator.History(new() { StartDate = new DateTime(2026, 11, 10) }));
        Assert.Throws<WeatherException>(() => WeatherValidator.History(new() { Page = int.MaxValue, PageSize = 100 }));
        Assert.Throws<WeatherException>(() => WeatherValidator.History(new() { Page = 0 }));
        Assert.Throws<WeatherException>(() => WeatherValidator.History(new() { StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(-1) }));
        WeatherValidator.History(new GetWeatherHistoryRequest { StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow });
    }
}
