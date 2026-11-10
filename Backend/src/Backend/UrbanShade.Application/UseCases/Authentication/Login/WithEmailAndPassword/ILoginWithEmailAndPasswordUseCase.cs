using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Communication.Responses.Authentication;

namespace UrbanShade.Application.UseCases.Authentication.Login.WithEmailAndPassword;

public interface ILoginWithEmailAndPasswordUseCase
{
    Task<AuthenticationResponse> Execute(LoginRequest request, CancellationToken cancellationToken = default);
}
