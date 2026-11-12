using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Security;

namespace UrbanShade.Infraestructure.Security;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly FirstAccessSettings _firstAccess;

    public JwtTokenService(JwtSettings settings, FirstAccessSettings? firstAccess = null)
    {
        _settings = settings;
        _firstAccess = firstAccess ?? new FirstAccessSettings();
    }

    public AccessToken CreateAccessToken(User user, Guid sessionId, IReadOnlyCollection<Guid>? organizationIds = null) =>
        sessionId == Guid.Empty ? throw new ArgumentException("A registered session is required.", nameof(sessionId)) :
        CreateToken(user, "access", DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes), organizationIds, sessionId);

    public AccessToken CreateFirstAccessToken(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_firstAccess.TokenMinutes);
        if (user.TemporaryPasswordExpiresAt.HasValue && user.TemporaryPasswordExpiresAt.Value < expires)
            expires = user.TemporaryPasswordExpiresAt.Value;
        return CreateToken(user, "first_access", expires, null);
    }

    private AccessToken CreateToken(User user, string purpose, DateTime expires, IReadOnlyCollection<Guid>? organizationIds, Guid? sessionId = null)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            IssuedAt = now, NotBefore = now, Expires = expires,
            Issuer = _settings.Issuer, Audience = _settings.Audience,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                ["role"] = purpose == "access" ? user.Role.ToString() : "FirstAccess",
                ["purpose"] = purpose,
                ["security_version"] = user.SecurityVersion,
                ["sid"] = sessionId?.ToString() ?? string.Empty,
                ["organization_ids"] = organizationIds?.Select(id => id.ToString()).ToArray() ?? []
            },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey)), SecurityAlgorithms.HmacSha256)
        };
        return new(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }

    public RefreshToken CreateRefreshToken()
    {
        var value = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return new(value, HashRefreshToken(value), DateTime.UtcNow.AddDays(_settings.RefreshTokenDays));
    }

    public string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
