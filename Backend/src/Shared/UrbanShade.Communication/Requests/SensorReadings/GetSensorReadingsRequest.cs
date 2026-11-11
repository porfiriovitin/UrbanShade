namespace UrbanShade.Communication.Requests.SensorReadings;

public class GetSensorReadingsRequest
{
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
