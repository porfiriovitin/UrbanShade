using System.Net;
using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Security;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Authentication.Logout;

public sealed class LogoutUseCase : ILogoutUseCase
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenService _tokens;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutUseCase(IRefreshTokenRepository refreshTokens, ITokenService tokens, IUnitOfWork unitOfWork)
    {
        _refreshTokens = refreshTokens;
        _tokens = tokens;
        _unitOfWork = unitOfWork;
    }

    public async Task Execute(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        /// :: Validate the request using FluentValidation.
        Validate(request);

        /// :: Start a transaction to ensure atomicity of the logout operation.
        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);

        /// :: Find the refresh token in the database for update to prevent race conditions.
        var token = await _refreshTokens.FindForUpdate(_tokens.HashRefreshToken(request.RefreshToken), cancellationToken);

        /// :: If the token exists, revoke all tokens in the same family to log out from all sessions.
        if (token is not null)
            await _refreshTokens.RevokeFamily(token.FamilyId, DateTime.UtcNow, cancellationToken);

        /// :: Commit the transaction to persist the changes.
        await transaction.Commit(cancellationToken);
    }

    private static void Validate(RefreshTokenRequest request)
    {
        var result = new LogoutValidator().Validate(request);
        if (!result.IsValid)
            throw new AuthenticationException(HttpStatusCode.Unauthorized, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }
}
