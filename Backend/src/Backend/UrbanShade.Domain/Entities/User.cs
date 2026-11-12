using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Entities;

public class User : EntityBase
{
    private User() { }

    public User(string name, string email, string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150) throw new ArgumentException("A valid name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 254 || !System.Net.Mail.MailAddress.TryCreate(email.Trim(), out var address) || address.Address != email.Trim()) throw new ArgumentException("A valid email is required.", nameof(email));
        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(hashedPassword)) throw new ArgumentException("A password hash is required.", nameof(hashedPassword));
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
        if (string.IsNullOrWhiteSpace(hashedPassword)) throw new ArgumentException("A password hash is required.", nameof(hashedPassword));
        HashedPassword = hashedPassword;
        MustChangePassword = false;
        TemporaryPasswordExpiresAt = null;
        SecurityVersion++;
    }

    public void SetTemporaryPassword(string hashedPassword, DateTime expiresAt)
    {
        if (expiresAt.Kind != DateTimeKind.Utc) throw new ArgumentException("Expiration must be UTC.", nameof(expiresAt));
        if (string.IsNullOrWhiteSpace(hashedPassword)) throw new ArgumentException("A password hash is required.", nameof(hashedPassword));
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

