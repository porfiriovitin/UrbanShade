namespace UrbanShade.Communication.Responses.Weather;

public sealed record WeatherAttribution
{
    public string Provider { get; init; } = "Open-Meteo";
    public string Url { get; init; } = "https://open-meteo.com/";
    public string License { get; init; } = "CC BY 4.0";
    public string LicenseUrl { get; init; } = "https://creativecommons.org/licenses/by/4.0/";
    public string DataNature { get; init; } = "MODEL_ESTIMATE";
}

public sealed record WeatherUnits
{
    public string Temperature { get; init; } = "°C";
    public string Humidity { get; init; } = "%";
    public string WindSpeed { get; init; } = "km/h";
    public string Precipitation { get; init; } = "mm";
    public string PrecipitationProbability { get; init; } = "%";
    public string CloudCover { get; init; } = "%";
    public string UvIndex { get; init; } = "index";
    public string SolarRadiation { get; init; } = "W/m²";
}

public sealed record WeatherCurrentResponse(Guid RegionId, string RegionName, string DataStatus, decimal? Temperature, decimal? Humidity, decimal? ApparentTemperature, decimal? WindSpeed, decimal? Precipitation, decimal? CloudCover, int? WeatherCode, DateTime? ObservedAt, DateTime? FetchedAt, double? Latitude, double? Longitude, int? BoundaryVersion)
{
    public string Source { get; init; } = "OPEN_METEO";
    public string? Model { get; init; }
    public WeatherAttribution Attribution { get; init; } = new();
    public WeatherUnits Units { get; init; } = new();
}

public sealed record WeatherHourlyResponse(DateTime ForecastAt, DateTime FetchedAt, decimal? Temperature, decimal? Humidity, decimal? ApparentTemperature, decimal? PrecipitationProbability, decimal? WindSpeed, decimal? CloudCover, decimal? UvIndex, decimal? SolarRadiation);
public sealed record WeatherForecastResponse(Guid RegionId, string DataStatus, DateTime? FetchedAt, double? Latitude, double? Longitude, int BoundaryVersion, List<WeatherHourlyResponse> Hourly)
{
    public string Source { get; init; } = "OPEN_METEO";
    public WeatherAttribution Attribution { get; init; } = new();
    public WeatherUnits Units { get; init; } = new();
}

public sealed record WeatherOverviewResponse(int TotalRegions, int CurrentRegions, int StaleRegions, int NoDataRegions, decimal? AverageTemperature, decimal? AverageHumidity, decimal? AverageApparentTemperature, List<WeatherCurrentResponse> Regions);
