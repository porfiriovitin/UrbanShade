using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Entities;

public class User : EntityBase
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string HashedPassword { get; private set; } = string.Empty;
    public UserRoles Role { get; private set; } = UserRoles.User;

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
        Role = role;
    }

}

