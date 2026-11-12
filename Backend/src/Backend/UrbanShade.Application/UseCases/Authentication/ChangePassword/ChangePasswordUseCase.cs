using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Security;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Authentication.ChangePassword;

public sealed class ChangePasswordUseCase : IChangePasswordUseCase
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwords;

    public ChangePasswordUseCase(IAccountRepository accounts, IUnitOfWork unitOfWork, IPasswordHasher passwords)
    {
        _accounts = accounts;
        _unitOfWork = unitOfWork;
        _passwords = passwords;
    }

    public async Task Execute(Guid userId, int securityVersion, ChangePasswordRequest request, bool firstAccess, CancellationToken cancellationToken = default)
    {
        var result = new ChangePasswordValidator(firstAccess).Validate(request);
        if (!result.IsValid)
            throw new AuthenticationException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);

        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);
        var user = await _accounts.FindForUpdate(userId, cancellationToken);
        if (user is null || !user.Active || user.SecurityVersion != securityVersion || user.MustChangePassword != firstAccess ||
            (firstAccess && (!user.TemporaryPasswordExpiresAt.HasValue || user.TemporaryPasswordExpiresAt <= DateTime.UtcNow)))
            throw new AuthenticationException(ErrorKind.Unauthenticated, "Account or session is unavailable.");

        if (!firstAccess && !await _passwords.Verify(request.CurrentPassword, user.HashedPassword))
            throw new AuthenticationException(ErrorKind.Unauthenticated, "Current password is invalid.");
        if (await _passwords.Verify(request.NewPassword, user.HashedPassword))
            throw new AuthenticationException(ErrorKind.Validation, "New password must differ from the current password.");

        /// :: Consume first-access credentials and invalidate every previous session atomically.
        user.UpdatePassword(await _passwords.Hash(request.NewPassword));
        await _accounts.RevokeSessions(user.Id, DateTime.UtcNow, cancellationToken);
        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
    }
}
