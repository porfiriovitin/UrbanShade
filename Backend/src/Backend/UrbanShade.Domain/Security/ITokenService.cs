using UrbanShade.Domain.Entities;

namespace UrbanShade.Domain.Security;

public record AccessToken
{
    public string Value { get; init; }
    public DateTime ExpiresAt { get; init; }

    public AccessToken(string Value, DateTime ExpiresAt)
    {
        this.Value = Value;
        this.ExpiresAt = ExpiresAt;
    }

    public void Deconstruct(out string Value, out DateTime ExpiresAt)
    {
        Value = this.Value;
        ExpiresAt = this.ExpiresAt;
    }
}

public record RefreshToken
{
    public string Value { get; init; }
    public string Hash { get; init; }
    public DateTime ExpiresAt { get; init; }

    public RefreshToken(string Value, string Hash, DateTime ExpiresAt)
    {
        this.Value = Value;
        this.Hash = Hash;
        this.ExpiresAt = ExpiresAt;
    }

    public void Deconstruct(out string Value, out string Hash, out DateTime ExpiresAt)
    {
        Value = this.Value;
        Hash = this.Hash;
        ExpiresAt = this.ExpiresAt;
    }
}

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
    RefreshToken CreateRefreshToken();
    string HashRefreshToken(string token);
}
