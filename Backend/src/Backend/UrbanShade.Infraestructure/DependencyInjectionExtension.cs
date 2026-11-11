using UrbanShade.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UrbanShade.Domain.Security;
using UrbanShade.Domain.Simulation;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.DataAcess.Repositories;
using UrbanShade.Infraestructure.Security;
using UrbanShade.Infraestructure.Workers;

namespace UrbanShade.Infraestructure;

public static class DependencyInjectionExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddDbContext(services, configuration);
        var jwt = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        jwt.Validate();
        services.AddSingleton(jwt);
        var firstAccess = configuration.GetSection("FirstAccess").Get<FirstAccessSettings>() ?? new FirstAccessSettings();
        firstAccess.Validate();
        services.AddSingleton(firstAccess);
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ISensorRepository, SensorRepository>();
        services.AddScoped<ISensorReadingRepository, SensorReadingRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        /// :: Set up sensor simulation settings and validate them.
        var simulation = configuration.GetSection("SensorSimulation").Get<SensorSimulationSettings>() ?? new SensorSimulationSettings();
        simulation.Validate();
        services.AddSingleton(simulation);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Random.Shared);
        services.AddHostedService<SensorSimulationBackgroundService>();
    }

    private static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<UrbanShadeDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DbConnection");
            options.UseNpgsql(connectionString);
            options.AddInterceptors(new PersistenceExceptionInterceptor());
        });
    }

}
