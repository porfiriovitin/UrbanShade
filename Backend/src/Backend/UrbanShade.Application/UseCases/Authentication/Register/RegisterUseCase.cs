using System.Net;
using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Communication.Responses.Authentication;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Security;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Authentication.Register;

public sealed class RegisterUseCase : IRegisterUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwords;
    private readonly TokenIssuer _issuer;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterUseCase(IUserRepository users, IPasswordHasher passwords, TokenIssuer issuer, IUnitOfWork unitOfWork)
    {
        _users = users;
        _passwords = passwords;
        _issuer = issuer;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthenticationResponse> Execute(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        /// :: Validate the request.
        Validate(request);

        /// :: Normalize the email to lowercase and trim whitespace for consistent lookup.
        var email = request.Email.Trim().ToLowerInvariant();

        /// :: Check if the email is already registered. 
        if (await _users.FindByEmail(email, cancellationToken) is not null)
            throw new EmailAlreadyRegisteredException();

        var passwordHash = await _passwords.Hash(request.Password);

        /// :: Create a new user entity with the provided name, normalized email, and hashed password.
        var user = new User(request.Name, email, passwordHash);

        /// :: Add the new user to the repository.
        _users.Add(user);

        /// :: Issue a new authentication token for the registered user and save the changes.
        AuthenticationResponse response = _issuer.Issue(user);
        await _unitOfWork.SaveChanges(cancellationToken);

        return response;
    }

    private static void Validate(RegisterRequest request)
    {
        var result = new RegisterValidator().Validate(request);
        if (!result.IsValid)
            throw new AuthenticationException(HttpStatusCode.BadRequest, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }
}
