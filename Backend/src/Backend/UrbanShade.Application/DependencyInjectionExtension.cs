using Microsoft.Extensions.DependencyInjection;
using UrbanShade.Application.UseCases.Authentication;
using UrbanShade.Application.UseCases.Authentication.Login.WithEmailAndPassword;
using UrbanShade.Application.UseCases.Authentication.Register;
using UrbanShade.Application.UseCases.Authentication.Refresh;
using UrbanShade.Application.UseCases.Authentication.Logout;
using UrbanShade.Application.UseCases.Authentication.Me;

namespace UrbanShade.Application;

public static class DependencyInjectionExtension
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TokenIssuer>();
        services.AddScoped<IRegisterUseCase, RegisterUseCase>();
        services.AddScoped<ILoginWithEmailAndPasswordUseCase, LoginWithEmailAndPasswordUseCase>();
        services.AddScoped<IRefreshUseCase, RefreshUseCase>();
        services.AddScoped<ILogoutUseCase, LogoutUseCase>();
        services.AddScoped<IGetCurrentUserUseCase, GetCurrentUserUseCase>();
    }
}
