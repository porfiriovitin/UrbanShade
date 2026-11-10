using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Communication.Responses.Authentication;

namespace UrbanShade.Application.UseCases.Authentication.Refresh;

public interface IRefreshUseCase
{
    Task<AuthenticationResponse> Execute(RefreshTokenRequest request, CancellationToken cancellationToken = default);
}
