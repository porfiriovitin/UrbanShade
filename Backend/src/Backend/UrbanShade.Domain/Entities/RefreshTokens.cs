namespace UrbanShade.Domain.Entities;

public class RefreshTokens
{
    private RefreshTokens() { }

    public RefreshTokens(string tokenHash, Guid userId, DateTime expiresAt, Guid? familyId = null, int securityVersion = 0)
    {
        Token = tokenHash;
        UserId = userId;
        ExpiresAt = expiresAt;
        FamilyId = familyId ?? Guid.NewGuid();
        SecurityVersion = securityVersion;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; } = DateTime.UtcNow.AddDays(7);
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public int SecurityVersion { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public bool IsUsable(DateTime now) => RevokedAt is null && ExpiresAt > now;
    public void Revoke(DateTime now) => RevokedAt ??= now;
}
