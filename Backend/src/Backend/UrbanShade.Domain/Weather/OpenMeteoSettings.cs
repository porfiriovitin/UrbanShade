namespace UrbanShade.Domain.Weather;

public sealed class OpenMeteoSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://api.open-meteo.com/v1/";
    public bool AutomaticRefreshEnabled { get; set; } = true;
    public int RefreshIntervalMinutes { get; set; } = 60;
    public int MinimumRefreshIntervalMinutes { get; set; } = 5;
    public int ForecastDays { get; set; } = 3;
    public int StaleDataThresholdMinutes { get; set; } = 120;
    public int CacheDurationMinutes { get; set; } = 15;
    public int TimeoutSeconds { get; set; } = 15;
    public int MaxRetryAttempts { get; set; } = 3;

    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("OpenMeteo requires an absolute HTTPS base URL without credentials, query or fragment.");
        if (ForecastDays is < 1 or > 16 || MinimumRefreshIntervalMinutes < 1 || RefreshIntervalMinutes < MinimumRefreshIntervalMinutes ||
            StaleDataThresholdMinutes < RefreshIntervalMinutes || CacheDurationMinutes is < 0 or > 15 ||
            TimeoutSeconds is < 1 or > 120 || MaxRetryAttempts is < 0 or > 3)
            throw new InvalidOperationException("Invalid OpenMeteo refresh, forecast, cache, timeout or retry settings.");
    }
}
