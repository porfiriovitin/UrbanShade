namespace UrbanShade.Communication.Responses.Authentication;

public record UserResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Email { get; init; }
    public string Role { get; init; }

    public UserResponse(Guid Id, string Name, string Email, string Role)
    {
        this.Id = Id;
        this.Name = Name;
        this.Email = Email;
        this.Role = Role;
    }

    public void Deconstruct(out Guid Id, out string Name, out string Email, out string Role)
    {
        Id = this.Id;
        Name = this.Name;
        Email = this.Email;
        Role = this.Role;
    }
}

public record AuthenticationResponse
{
    public bool MustChangePassword { get; init; }
    public string? FirstAccessToken { get; init; }
    public string? FirstAccessTokenExpiresAt { get; init; }
    public List<Guid> OrganizationIds { get; init; } = [];
    public string AccessToken { get; init; }
    public string AccessTokenExpiresAt { get; init; }
    public string RefreshToken { get; init; }
    public string RefreshTokenExpiresAt { get; init; }
    public UserResponse User { get; init; }

    public AuthenticationResponse(string AccessToken, string AccessTokenExpiresAt, string RefreshToken, string RefreshTokenExpiresAt, UserResponse User)
    {
        this.AccessToken = AccessToken;
        this.AccessTokenExpiresAt = AccessTokenExpiresAt;
        this.RefreshToken = RefreshToken;
        this.RefreshTokenExpiresAt = RefreshTokenExpiresAt;
        this.User = User;
    }

    public void Deconstruct(out string AccessToken, out string AccessTokenExpiresAt, out string RefreshToken, out string RefreshTokenExpiresAt, out UserResponse User)
    {
        AccessToken = this.AccessToken;
        AccessTokenExpiresAt = this.AccessTokenExpiresAt;
        RefreshToken = this.RefreshToken;
        RefreshTokenExpiresAt = this.RefreshTokenExpiresAt;
        User = this.User;
    }
}
