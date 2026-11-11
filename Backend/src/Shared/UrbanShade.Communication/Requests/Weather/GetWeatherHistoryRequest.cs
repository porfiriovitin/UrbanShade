namespace UrbanShade.Communication.Requests.Weather;

public sealed class GetWeatherHistoryRequest
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
