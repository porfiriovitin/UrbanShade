namespace UrbanShade.Communication.Responses.Sensors;

public record SensorResponse
{
    public Guid Id { get; init; }
    public Guid? OrganizationId { get; init; }
    public Guid? CityId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? InstalledAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}
