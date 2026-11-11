namespace UrbanShade.Communication.Requests.Organizations;

public class OrganizationRequest
{
    public string TimeZoneId { get; init; } = "America/Sao_Paulo";
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Document { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public string? ContactPhone { get; init; }
}

public class UpdateOrganizationRequest : OrganizationRequest
{
    public bool Active { get; init; } = true;
}
