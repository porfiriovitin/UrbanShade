using Microsoft.Extensions.DependencyInjection;
using UrbanShade.Application.UseCases.SensorReadings.Generate;
using UrbanShade.Application.UseCases.SensorReadings.GetHistory;
using UrbanShade.Application.UseCases.SensorReadings.GetLatest;
using UrbanShade.Application.UseCases.SensorReadings.GetLatestBySensors;
using UrbanShade.Application.UseCases.Sensors.Register;
using UrbanShade.Application.UseCases.Sensors.Update;
using UrbanShade.Application.UseCases.Sensors.UpdateStatus;
using UrbanShade.Application.UseCases.Sensors.Delete;
using UrbanShade.Application.UseCases.Sensors.GetAll;
using UrbanShade.Application.UseCases.Sensors.GetById;
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
        services.AddScoped<SensorReadingGenerator>();
        services.AddScoped<IGenerateSensorReadingsUseCase, GenerateSensorReadingsUseCase>();
        services.AddScoped<IGetSensorReadingsUseCase, GetSensorReadingsUseCase>();
        services.AddScoped<IGetLatestSensorReadingUseCase, GetLatestSensorReadingUseCase>();
        services.AddScoped<IGetLatestSensorReadingsUseCase, GetLatestSensorReadingsUseCase>();
        services.AddScoped<IRegisterSensorUseCase, RegisterSensorUseCase>();
        services.AddScoped<IUpdateSensorUseCase, UpdateSensorUseCase>();
        services.AddScoped<IUpdateSensorStatusUseCase, UpdateSensorStatusUseCase>();
        services.AddScoped<IDeleteSensorUseCase, DeleteSensorUseCase>();
        services.AddScoped<IGetSensorsUseCase, GetSensorsUseCase>();
        services.AddScoped<IGetSensorByIdUseCase, GetSensorByIdUseCase>();
        services.AddScoped<IRegisterUseCase, RegisterUseCase>();
        services.AddScoped<ILoginWithEmailAndPasswordUseCase, LoginWithEmailAndPasswordUseCase>();
        services.AddScoped<IRefreshUseCase, RefreshUseCase>();
        services.AddScoped<ILogoutUseCase, LogoutUseCase>();
        services.AddScoped<IGetCurrentUserUseCase, GetCurrentUserUseCase>();
    }
}
