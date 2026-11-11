using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Entities;

public class Sensor : EntityBase
{
    private Sensor() { }

    public Sensor(string code, string name, string? description, double latitude, double longitude, DateTime? installedAt)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim();
        Latitude = latitude;
        Longitude = longitude;
        InstalledAt = installedAt;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public SensorStatus Status { get; private set; } = SensorStatus.ACTIVE;
    public Guid? OrganizationId { get; private set; }
    public DateTime? InstalledAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public void Update(string code, string name, string? description, double latitude, double longitude, DateTime? installedAt)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim();
        Latitude = latitude;
        Longitude = longitude;
        InstalledAt = installedAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(SensorStatus status)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        Active = false;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DeletedAt;
    }

    public void AssignOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("An organization is required.", nameof(organizationId));
        if (OrganizationId.HasValue && OrganizationId.Value != organizationId)
            throw new InvalidOperationException("Sensor transfers between organizations are not supported.");
        OrganizationId = organizationId;
        UpdatedAt = DateTime.UtcNow;
    }
}
