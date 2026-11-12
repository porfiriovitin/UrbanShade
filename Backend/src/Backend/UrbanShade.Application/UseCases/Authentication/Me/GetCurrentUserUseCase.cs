using UrbanShade.Communication.Responses.Authentication;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Authentication.Me;

public sealed class GetCurrentUserUseCase : IGetCurrentUserUseCase
{
    private readonly IUserRepository _users;

    public GetCurrentUserUseCase(IUserRepository users)
    {
        _users = users;
    }

    public async Task<UserResponse> Execute(Guid userId, CancellationToken cancellationToken = default)
    {
        /// :: Retrieve the user from the repository using the provided user ID.
        var user = await _users.FindById(userId, cancellationToken);

        /// :: If the user is not found or is inactive, throw an authentication exception.
        if (user is null || !user.Active)
            throw new AuthenticationException(ErrorKind.Unauthenticated, "User is unavailable.");

        /// :: Convert the user entity to a UserResponse DTO and return it.
        return TokenIssuer.ToResponse(user);
    }
}
