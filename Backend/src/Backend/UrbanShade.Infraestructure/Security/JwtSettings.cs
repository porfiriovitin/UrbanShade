using System.Text;

namespace UrbanShade.Infraestructure.Security;

public sealed class JwtSettings
{
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "UrbanShade";
    public string Audience { get; set; } = "UrbanShade.API";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || Encoding.UTF8.GetByteCount(SigningKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience) ||
            AccessTokenMinutes is < 1 or > 60 || RefreshTokenDays is < 1 or > 30)
            throw new InvalidOperationException("Invalid JWT issuer, audience or token lifetime.");
    }
}
