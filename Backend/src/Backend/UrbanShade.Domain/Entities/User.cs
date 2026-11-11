using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Entities;

public class User : EntityBase
{
    private User() { }

    public User(string name, string email, string hashedPassword)
    {
        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        HashedPassword = hashedPassword;
    }

    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string HashedPassword { get; private set; } = string.Empty;
    public UserRoles Role { get; private set; } = UserRoles.Citizen;
    public bool MustChangePassword { get; private set; }
    public DateTime? TemporaryPasswordExpiresAt { get; private set; }
    public int SecurityVersion { get; private set; }

    public void Deactivate()
    {
        Active = false;
    }

    public void UpdatePassword(string hashedPassword)
    {
        HashedPassword = hashedPassword;
        MustChangePassword = false;
        TemporaryPasswordExpiresAt = null;
        SecurityVersion++;
    }

    public void SetTemporaryPassword(string hashedPassword, DateTime expiresAt)
    {
        HashedPassword = hashedPassword;
        MustChangePassword = true;
        TemporaryPasswordExpiresAt = expiresAt;
        SecurityVersion++;
    }

    public void UpdateRole(UserRoles role)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        Role = role;
    }

}

