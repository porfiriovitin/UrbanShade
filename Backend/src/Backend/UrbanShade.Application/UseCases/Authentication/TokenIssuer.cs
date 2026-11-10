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

    public AuthenticationResponse Issue(User user, Guid? familyId = null)
    {
        /// :: Create acess and refresh tokens for the user.
        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.CreateRefreshToken();

        /// :: If no family ID is provided, generate a new one for the refresh token.
        _refreshTokens.Add(new RefreshTokens(refresh.Hash, user.Id, refresh.ExpiresAt, familyId));

        return new(access.Value, access.ExpiresAt, refresh.Value, refresh.ExpiresAt, ToResponse(user));
    }

    public static UserResponse ToResponse(User user) => new(user.Id, user.Name, user.Email, user.Role.ToString());
}
