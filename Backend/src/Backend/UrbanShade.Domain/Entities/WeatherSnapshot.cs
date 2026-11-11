using UrbanShade.Domain.Weather;

namespace UrbanShade.Domain.Entities;

public sealed class WeatherSnapshot : EntityBase
{
    private WeatherSnapshot() { }
    public WeatherSnapshot(Guid geoRegionId, int boundaryVersion, double latitude, double longitude, WeatherObservation observation)
    {
        GeoRegionId = geoRegionId;
        BoundaryVersion = boundaryVersion;
        Latitude = latitude;
        Longitude = longitude;
        Temperature = observation.Current.Temperature;
        Humidity = observation.Current.Humidity;
        ApparentTemperature = observation.Current.ApparentTemperature;
        WindSpeed = observation.Current.WindSpeed;
        Precipitation = observation.Current.Precipitation;
        CloudCover = observation.Current.CloudCover;
        WeatherCode = observation.Current.WeatherCode;
        ObservedAt = observation.ObservedAt;
        FetchedAt = observation.FetchedAt;
    }
    public Guid GeoRegionId { get; private set; }
    public int BoundaryVersion { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public decimal? Temperature { get; private set; }
    public decimal? Humidity { get; private set; }
    public decimal? ApparentTemperature { get; private set; }
    public decimal? WindSpeed { get; private set; }
    public decimal? Precipitation { get; private set; }
    public decimal? CloudCover { get; private set; }
    public int? WeatherCode { get; private set; }
    public DateTime ObservedAt { get; private set; }
    public DateTime FetchedAt { get; private set; }
    public string Source { get; private set; } = "OPEN_METEO";
    public string? Model { get; private set; }
}
