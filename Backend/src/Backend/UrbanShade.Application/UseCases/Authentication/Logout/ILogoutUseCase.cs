using UrbanShade.Communication.Requests.Authentication;

namespace UrbanShade.Application.UseCases.Authentication.Logout;

public interface ILogoutUseCase
{
    Task Execute(RefreshTokenRequest request, CancellationToken cancellationToken = default);
}
