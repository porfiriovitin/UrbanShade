using UrbanShade.Domain.Weather;

namespace UrbanShade.Domain.Entities;

public sealed class WeatherForecast : EntityBase
{
    private WeatherForecast()
    {
    }

    public WeatherForecast(Guid geoRegionId, int boundaryVersion, double latitude, double longitude, WeatherPrediction prediction, DateTime fetchedAt, string source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > 32)
            throw new ArgumentException("A weather source is required.", nameof(source));

        Source = source;
        GeoRegionId = geoRegionId;
        ForecastAt = prediction.ForecastAt;
        Update(boundaryVersion, latitude, longitude, prediction, fetchedAt);
    }

    public Guid GeoRegionId { get; private set; }
    public int BoundaryVersion { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public decimal? Temperature { get; private set; }
    public decimal? Humidity { get; private set; }
    public decimal? ApparentTemperature { get; private set; }
    public decimal? PrecipitationProbability { get; private set; }
    public decimal? WindSpeed { get; private set; }
    public decimal? CloudCover { get; private set; }
    public decimal? UvIndex { get; private set; }
    public decimal? SolarRadiation { get; private set; }
    public DateTime ForecastAt { get; private set; }
    public DateTime FetchedAt { get; private set; }
    public string Source { get; private set; } = string.Empty;

    public void Update(int boundaryVersion, double latitude, double longitude, WeatherPrediction prediction, DateTime fetchedAt)
    {
        if (prediction.ForecastAt != ForecastAt || fetchedAt < FetchedAt) return;

        var sameBoundary = BoundaryVersion == boundaryVersion;
        Temperature = prediction.Temperature ?? (sameBoundary ? Temperature : null);
        Humidity = prediction.Humidity ?? (sameBoundary ? Humidity : null);
        ApparentTemperature = prediction.ApparentTemperature ?? (sameBoundary ? ApparentTemperature : null);
        PrecipitationProbability = prediction.PrecipitationProbability ?? (sameBoundary ? PrecipitationProbability : null);
        WindSpeed = prediction.WindSpeed ?? (sameBoundary ? WindSpeed : null);
        CloudCover = prediction.CloudCover ?? (sameBoundary ? CloudCover : null);
        UvIndex = prediction.UvIndex ?? (sameBoundary ? UvIndex : null);
        SolarRadiation = prediction.SolarRadiation ?? (sameBoundary ? SolarRadiation : null);
        BoundaryVersion = boundaryVersion;
        Latitude = latitude;
        Longitude = longitude;
        FetchedAt = fetchedAt;
    }
}
