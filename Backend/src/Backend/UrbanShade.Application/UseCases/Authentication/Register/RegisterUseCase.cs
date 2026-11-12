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
    private readonly IUnitOfWork _unitOfWork;

    public RegisterUseCase(IUserRepository users, IPasswordHasher passwords, IUnitOfWork unitOfWork)
    {
        _users = users;
        _passwords = passwords;
        _unitOfWork = unitOfWork;
    }

    public async Task Execute(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        /// :: Validate the request.
        Validate(request);

        /// :: Normalize the email to lowercase and trim whitespace for consistent lookup.
        var email = request.Email.Trim().ToLowerInvariant();

        // Perform the same password derivation for new and existing addresses.
        var passwordHash = await _passwords.Hash(request.Password);
        if (await _users.FindByEmail(email, cancellationToken) is not null)
            return;

        /// :: Create a new user entity with the provided name, normalized email, and hashed password.
        var user = new User(request.Name, email, passwordHash);

        /// :: Add the new user to the repository.
        _users.Add(user);

        // A concurrent registration must have the same public response.
        try { await _unitOfWork.SaveChanges(cancellationToken); }
        catch (EmailAlreadyRegisteredException) { }
    }

    private static void Validate(RegisterRequest request)
    {
        var result = new RegisterValidator().Validate(request);
        if (!result.IsValid)
            throw new AuthenticationException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }
}
