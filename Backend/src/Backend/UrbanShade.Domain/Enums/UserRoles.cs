namespace UrbanShade.Domain.Enums;

public enum UserRoles
{
    /// <summary>Operates the platform, including organizations and simulated sensors.</summary>
    PlatformAdministrator,
    /// <summary>Consults city conditions and manages personal preferences.</summary>
    Citizen,
    /// <summary>Manages proposals and consults resources within authorized organizations.</summary>
    Manager
}
