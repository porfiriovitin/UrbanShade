namespace UrbanShade.Domain.Entities;

public class SensorReading : EntityBase
{
    private SensorReading() { }

    public SensorReading(Guid sensorId, decimal temperature, decimal humidity, DateTime measuredAt)
    {
        if (sensorId == Guid.Empty) throw new ArgumentException("A sensor is required.", nameof(sensorId));
        if (humidity < 0 || humidity > 100) throw new ArgumentOutOfRangeException(nameof(humidity));
        if (measuredAt.Kind != DateTimeKind.Utc) throw new ArgumentException("MeasuredAt must be UTC.", nameof(measuredAt));

        SensorId = sensorId;
        Temperature = temperature;
        Humidity = humidity;
        MeasuredAt = measuredAt;
    }

    public Guid SensorId { get; private set; }
    public decimal Temperature { get; private set; }
    public decimal Humidity { get; private set; }
    public DateTime MeasuredAt { get; private set; }
}
