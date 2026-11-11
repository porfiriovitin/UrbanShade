namespace UrbanShade.Communication.Responses.Cities;

public sealed record CityResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string IbgeCode { get; init; } = string.Empty;
}
