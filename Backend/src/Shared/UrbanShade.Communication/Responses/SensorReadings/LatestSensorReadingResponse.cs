namespace UrbanShade.Communication.Responses.SensorReadings;

public record LatestSensorReadingResponse : SensorReadingResponse
{
    public string SensorName { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
