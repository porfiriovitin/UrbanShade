using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Communication.Responses.Authentication;

namespace UrbanShade.Application.UseCases.Authentication.Register;

public interface IRegisterUseCase
{
    Task<AuthenticationResponse> Execute(RegisterRequest request, CancellationToken cancellationToken = default);
}
