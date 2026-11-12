using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Communication.Responses.Authentication;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Security;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Authentication.Refresh;

public sealed class RefreshUseCase : IRefreshUseCase
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUserRepository _users;
    private readonly ITokenService _tokens;
    private readonly TokenIssuer _issuer;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrganizationRepository? _organizations;

    public RefreshUseCase(IRefreshTokenRepository refreshTokens, IUserRepository users, ITokenService tokens, TokenIssuer issuer, IUnitOfWork unitOfWork, IOrganizationRepository? organizations = null)
    {
        _refreshTokens = refreshTokens;
        _users = users;
        _tokens = tokens;
        _issuer = issuer;
        _unitOfWork = unitOfWork;
        _organizations = organizations;
    }

    public async Task<AuthenticationResponse> Execute(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        /// :: Validates the request.
        Validate(request);

        /// :: Start a transaction to ensure atomicity of the refresh operation.
        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);

        /// :: Find the refresh token in the database for update to prevent race conditions.
        var token = await _refreshTokens.FindForUpdate(_tokens.HashRefreshToken(request.RefreshToken), cancellationToken);

        /// :: If the token is not found, throw an invalid token exception.
        if (token is null)
            throw InvalidToken();

        /// :: Get the current UTC time and retrieve the user associated with the refresh token.
        var now = DateTime.UtcNow;

        /// :: If the token is not usable or the user is not found or inactive, revoke all tokens in the same family and throw an invalid token exception.
        var user = await _users.FindById(token.UserId, cancellationToken);
        if (!token.IsUsable(now) || user is null || !user.Active || user.MustChangePassword || token.SecurityVersion != user.SecurityVersion)
        {
            await _refreshTokens.RevokeFamily(token.FamilyId, now, cancellationToken);
            await transaction.Commit(cancellationToken);
            throw InvalidToken();
        }

        /// :: Revoke the current refresh token to prevent reuse.
        token.Revoke(now);

        /// :: Issue new access and refresh tokens for the user, preserving the family ID for session continuity.
        var organizationIds = _organizations is null ? [] : await _organizations.GetActiveIdsForUser(user.Id, cancellationToken);
        AuthenticationResponse response = _issuer.Issue(user, token.FamilyId, organizationIds);

        /// :: Save the changes to the database and commit the transaction to persist the new tokens and revocation.
        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);

        return response;
    }

    private static void Validate(RefreshTokenRequest request)
    {
        var result = new RefreshValidator().Validate(request);
        if (!result.IsValid)
            throw new AuthenticationException(ErrorKind.Unauthenticated, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }

    private static AuthenticationException InvalidToken() => new(ErrorKind.Unauthenticated, "Invalid or expired refresh token.");
}
