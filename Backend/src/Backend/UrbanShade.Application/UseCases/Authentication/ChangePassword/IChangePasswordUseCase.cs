using UrbanShade.Communication.Requests.Authentication;

namespace UrbanShade.Application.UseCases.Authentication.ChangePassword;

public interface IChangePasswordUseCase
{
    Task Execute(Guid userId, int securityVersion, ChangePasswordRequest request, bool firstAccess, CancellationToken cancellationToken = default);
}
