using System.Net;
using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Communication.Responses.Authentication;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Security;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Authentication.Login.WithEmailAndPassword;

public sealed class LoginWithEmailAndPasswordUseCase : ILoginWithEmailAndPasswordUseCase
{
    ///  :: Dummy Hash to avoid deductions based on timing differences during account enumeration.
    private const string DummyHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwords;
    private readonly TokenIssuer _issuer;
    private readonly IUnitOfWork _unitOfWork;

    public LoginWithEmailAndPasswordUseCase(IUserRepository users, IPasswordHasher passwords, TokenIssuer issuer, IUnitOfWork unitOfWork)
    {
        _users = users;
        _passwords = passwords;
        _issuer = issuer;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthenticationResponse> Execute(LoginRequest request, CancellationToken cancellationToken = default)
    {
        /// :: Validate the request using FluentValidation.
        Validate(request);

        /// :: Normalize the email to lowercase and trim whitespace for consistent lookup.
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _users.FindByEmail(email, cancellationToken);

        /// :: If user not found, use dummy hash to avoid timing attacks.
        var hash = user?.HashedPassword ?? DummyHash;

        /// :: Verify password against the hash (real or dummy).
        var valid = await _passwords.Verify(request.Password, hash);

        if (user is null || !valid || !user.Active)
            throw new AuthenticationException(HttpStatusCode.Unauthorized, "Invalid email or password.");

        /// :: Issue tokens for the authenticated user.
        AuthenticationResponse response = _issuer.Issue(user);

        /// :: Save the refresh token to the database.
        await _unitOfWork.SaveChanges(cancellationToken);

        return response;
    }

    private static void Validate(LoginRequest request)
    {
        var result = new LoginValidator().Validate(request);
        if (!result.IsValid)
            throw new AuthenticationException(HttpStatusCode.BadRequest, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }
}
