namespace UrbanShade.Domain.Weather;

public interface IWeatherProvider
{
    Task<WeatherObservation> GetWeather(double latitude, double longitude, CancellationToken cancellationToken);
}

public sealed record WeatherConditions(decimal? Temperature, decimal? Humidity, decimal? ApparentTemperature,
    decimal? WindSpeed, decimal? Precipitation, decimal? CloudCover, int? WeatherCode);

public sealed record WeatherPrediction(DateTime ForecastAt, decimal? Temperature, decimal? Humidity, decimal? ApparentTemperature,
    decimal? PrecipitationProbability, decimal? WindSpeed, decimal? CloudCover, decimal? UvIndex, decimal? SolarRadiation);

public sealed record WeatherObservation(DateTime ObservedAt, DateTime FetchedAt, WeatherConditions Current, IReadOnlyList<WeatherPrediction> Hourly)
{
    public string Source { get; init; } = "UNKNOWN";
}

public sealed class WeatherProviderException(string message, DateTime? retryAt = null) : Exception(message)
{
    public DateTime? RetryAt { get; } = retryAt;
}
