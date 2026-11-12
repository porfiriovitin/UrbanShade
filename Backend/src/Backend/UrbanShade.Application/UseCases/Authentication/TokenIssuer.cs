using System.Globalization;
using UrbanShade.Communication.Responses.Authentication;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Security;

namespace UrbanShade.Application.UseCases.Authentication;

public sealed class TokenIssuer
{
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenRepository _refreshTokens;

    public TokenIssuer(ITokenService tokens, IRefreshTokenRepository refreshTokens)
    {
        _tokens = tokens;
        _refreshTokens = refreshTokens;
    }

    public AuthenticationResponse Issue(User user, Guid? familyId = null, IReadOnlyCollection<Guid>? organizationIds = null)
    {
        if (user.MustChangePassword)
        {
            var firstAccess = _tokens.CreateFirstAccessToken(user);
            return new AuthenticationResponse(string.Empty, string.Empty, string.Empty, string.Empty, ToResponse(user))
            {
                MustChangePassword = true,
                FirstAccessToken = firstAccess.Value,
                FirstAccessTokenExpiresAt = firstAccess.ExpiresAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
            };
        }

        /// :: Create acess and refresh tokens for the user.
        var sessionId = familyId ?? Guid.NewGuid();
        var access = _tokens.CreateAccessToken(user, sessionId, organizationIds);
        var refresh = _tokens.CreateRefreshToken();

        /// :: If no family ID is provided, generate a new one for the refresh token.
        _refreshTokens.Add(new RefreshTokens(refresh.Hash, user.Id, refresh.ExpiresAt, sessionId, user.SecurityVersion));

        /// :: Format token expiration dates for the authentication response.
        return new(access.Value, access.ExpiresAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            refresh.Value, refresh.ExpiresAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture), ToResponse(user))
        {
            OrganizationIds = organizationIds?.ToList() ?? []
        };
    }

    public static UserResponse ToResponse(User user) => new(user.Id, user.Name, user.Email, user.Role.ToString());
}
