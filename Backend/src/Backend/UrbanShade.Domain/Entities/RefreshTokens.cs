namespace UrbanShade.Domain.Entities;

public class RefreshTokens
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; } = DateTime.UtcNow.AddDays(7);
    public Guid UserId { get; private set; }
}
