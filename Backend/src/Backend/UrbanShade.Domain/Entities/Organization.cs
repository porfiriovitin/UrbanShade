using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Entities;

public class Organization : EntityBase
{
    private Organization() { }

    public Organization(string name, OrganizationType type, string document, string contactEmail, string? contactPhone)
    {
        Name = name.Trim();
        Type = type;
        Document = document.Trim().ToUpperInvariant();
        ContactEmail = contactEmail.Trim().ToLowerInvariant();
        ContactPhone = contactPhone?.Trim();
    }

    public string TimeZoneId { get; private set; } = "America/Sao_Paulo";

    public void UpdateTimeZone(string timeZoneId)
    {
        TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        TimeZoneId = TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var ianaId) ? ianaId : timeZoneId;
    }

    public string Name { get; private set; } = string.Empty;
    public OrganizationType Type { get; private set; }
    public string Document { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string? ContactPhone { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(string name, OrganizationType type, string document, string contactEmail, string? contactPhone, bool active)
    {
        Name = name.Trim();
        Type = type;
        Document = document.Trim().ToUpperInvariant();
        ContactEmail = contactEmail.Trim().ToLowerInvariant();
        ContactPhone = contactPhone?.Trim();
        Active = active;
        UpdatedAt = DateTime.UtcNow;
    }
}
