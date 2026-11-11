namespace UrbanShade.Communication.Responses.SensorReadings;

public record SensorReadingResponse
{
    public Guid Id { get; init; }
    public Guid SensorId { get; init; }
    public decimal Temperature { get; init; }
    public decimal Humidity { get; init; }
    public DateTime MeasuredAt { get; init; }
    public bool IsSimulated { get; init; } = true;
}
