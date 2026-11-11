namespace UrbanShade.Communication.Requests.Cities;

public sealed class RegisterCityRequest
{
    public string Name { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string IbgeCode { get; init; } = string.Empty;
}
