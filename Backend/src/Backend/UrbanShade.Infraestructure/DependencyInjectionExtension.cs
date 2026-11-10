using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.DataAcess.Repositories;
using UrbanShade.Infraestructure.Security;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Security;

namespace UrbanShade.Infraestructure;

public static class DependencyInjectionExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddDbContext(services, configuration);
        var jwt = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        jwt.Validate();
        services.AddSingleton(jwt);
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISensorRepository, SensorRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
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
