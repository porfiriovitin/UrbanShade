using UrbanShade.Communication.Responses.Authentication;

namespace UrbanShade.Application.UseCases.Authentication.Me;

public interface IGetCurrentUserUseCase
{
    Task<UserResponse> Execute(Guid userId, CancellationToken cancellationToken = default);
}
