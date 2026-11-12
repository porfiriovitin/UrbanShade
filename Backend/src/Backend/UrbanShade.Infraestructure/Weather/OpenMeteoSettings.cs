using UrbanShade.Domain.Weather;
namespace UrbanShade.Infraestructure.Weather;
public sealed class OpenMeteoSettings : WeatherSettings
{
    public string BaseUrl { get; set; } = "https://api.open-meteo.com/v1/";
    public int TimeoutSeconds { get; set; } = 15;
    public int MaxRetryAttempts { get; set; } = 3;
    public override void Validate()
    {
        base.Validate();
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            TimeoutSeconds is < 1 or > 120 || MaxRetryAttempts is < 0 or > 3)
            throw new InvalidOperationException("Invalid Open-Meteo URL, timeout or retry settings.");
    }
}
