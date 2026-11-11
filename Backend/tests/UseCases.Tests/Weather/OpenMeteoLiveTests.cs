using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UrbanShade.Domain.Weather;
using UrbanShade.Infraestructure.Weather;
using Xunit.Abstractions;

namespace UseCases.Tests.Weather;

public sealed class OpenMeteoLiveFactAttribute : FactAttribute
{
    public OpenMeteoLiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("URBANSHADE_OPEN_METEO_TEST") != "true")
            Skip = "Set URBANSHADE_OPEN_METEO_TEST=true to run against the real Open-Meteo provider.";
    }
}

public sealed class OpenMeteoLiveTests(ITestOutputHelper output)
{
    [OpenMeteoLiveFact]
    public async Task RealProvider_ReturnsCurrentConditionsAndHourlyForecasts()
    {
        using var client = new HttpClient { BaseAddress = new Uri("https://api.open-meteo.com/v1/"), Timeout = Timeout.InfiniteTimeSpan };
        foreach (var days in new[] { 3, 16 })
        {
            var service = new OpenMeteoService(client, Options.Create(new OpenMeteoSettings { ForecastDays = days, MaxRetryAttempts = 0 }), TimeProvider.System, NullLogger<OpenMeteoService>.Instance);
            var observation = await service.GetWeather(-23.587, -46.656, default);
            Assert.NotNull(observation.Current.Temperature);
            Assert.NotNull(observation.Current.Humidity);
            Assert.NotNull(observation.Current.ApparentTemperature);
            Assert.NotEmpty(observation.Hourly);
            Assert.All(observation.Hourly, item => Assert.True(item.ForecastAt > observation.FetchedAt));
            Assert.Contains(observation.Hourly, item => item.UvIndex.HasValue && item.SolarRadiation.HasValue);
            output.WriteLine($"{days} days: {observation.Hourly.Count} future forecasts; {observation.Current.Temperature} °C, fetched at {observation.FetchedAt:O}.");
        }
    }
}
