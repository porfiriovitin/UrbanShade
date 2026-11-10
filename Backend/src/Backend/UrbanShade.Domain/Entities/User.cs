using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Entities;

public class User : EntityBase
{
    private User() { }

    public User(string name, string email, string hashedPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashedPassword);
        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        HashedPassword = hashedPassword;
    }

    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string HashedPassword { get; private set; } = string.Empty;
    public UserRoles Role { get; private set; } = UserRoles.Citizen;

    public void Deactivate()
    {
        Active = false;
    }

    public void UpdatePassword(string hashedPassword)
    {
        HashedPassword = hashedPassword;
    }

    public void UpdateRole(UserRoles role)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        Role = role;
    }

}

