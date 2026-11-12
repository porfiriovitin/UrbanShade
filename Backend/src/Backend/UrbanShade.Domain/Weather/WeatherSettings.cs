namespace UrbanShade.Domain.Weather;

public class WeatherSettings
{
    public bool Enabled { get; set; } = true;
    public bool AutomaticRefreshEnabled { get; set; } = true;
    public int RefreshIntervalMinutes { get; set; } = 60;
    public int MinimumRefreshIntervalMinutes { get; set; } = 5;
    public int ForecastDays { get; set; } = 3;
    public int StaleDataThresholdMinutes { get; set; } = 120;
    public int CacheDurationMinutes { get; set; } = 15;

    public virtual void Validate()
    {
        if (ForecastDays is < 1 or > 16 || MinimumRefreshIntervalMinutes < 1 || RefreshIntervalMinutes < MinimumRefreshIntervalMinutes || StaleDataThresholdMinutes < RefreshIntervalMinutes || CacheDurationMinutes is < 0 or > 15)
            throw new InvalidOperationException("Invalid weather refresh, forecast or cache settings.");
    }
}
