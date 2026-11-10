namespace UrbanShade.Communication.Requests.Sensors;

public class RegisterSensorRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public DateTime? InstalledAt { get; init; }
}
