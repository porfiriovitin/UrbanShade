namespace UrbanShade.Communication.Requests.Sensors;

public class GetSensorsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
    public string? Status { get; init; }
    public string? Code { get; init; }
    public string? Name { get; init; }
}
