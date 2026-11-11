namespace UrbanShade.Communication.Responses.Organizations;

public record OrganizationResponse
{
    public string TimeZoneId { get; init; } = string.Empty;
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Document { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public string? ContactPhone { get; init; }
    public bool Active { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
