using UrbanShade.Domain.Geospatial;
using UrbanShade.Infraestructure.Geospatial;
using UrbanShade.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UrbanShade.Domain.Security;
using UrbanShade.Domain.Simulation;
using UrbanShade.Domain.ThermalAnalysis;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.DataAcess.Repositories;
using UrbanShade.Infraestructure.Security;
using UrbanShade.Infraestructure.Workers;
using Microsoft.Extensions.Options;
using UrbanShade.Domain.Weather;
using UrbanShade.Infraestructure.Weather;

namespace UrbanShade.Infraestructure;

public static class DependencyInjectionExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddDbContext(services, configuration);
        var dashboard = configuration.GetSection("InstitutionalDashboard").Get<UrbanShade.Domain.InstitutionalDashboard.InstitutionalDashboardSettings>() ?? new();
        dashboard.Validate();
        services.AddSingleton(dashboard);
        services.AddScoped<IInstitutionalDashboardRepository, InstitutionalDashboardRepository>();
        var routing = configuration.GetSection("Routing").Get<UrbanShade.Domain.Routing.RoutingSettings>() ?? new();
        routing.Validate();
        services.AddSingleton(routing);
        services.AddOptions<UrbanShade.Infraestructure.Routing.OpenRouteServiceOptions>().Bind(configuration.GetSection("OpenRouteService"))
            .Validate(options => !routing.Enabled || !string.IsNullOrWhiteSpace(options.ApiKey), "OpenRouteService:Api_Key is required when routing is enabled.").ValidateOnStart();
        services.AddSingleton<UrbanShade.Infraestructure.Routing.OpenRouteServiceResponseParser>();
        services.AddSingleton<UrbanShade.Infraestructure.Routing.RoutingGeometryCache>();
        services.AddHttpClient<UrbanShade.Domain.Routing.IRoutingProvider, UrbanShade.Infraestructure.Routing.OpenRouteServiceProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .RedactLoggedHeaders(["Authorization"]);
        services.AddScoped<IRouteGeometryRepository, RouteGeometryRepository>();
        var regional = configuration.GetSection("RegionThermalAnalysis").Get<UrbanShade.Domain.RegionThermalAnalysis.RegionThermalAnalysisSettings>() ?? new();
        regional.Validate();
        services.AddSingleton(regional);
        services.AddScoped<IRegionThermalAnalysisRepository, RegionThermalAnalysisRepository>();
        services.AddHostedService<RegionThermalAnalysisBackgroundService>();
        services.AddOptions<OpenMeteoSettings>()
            .Bind(configuration.GetSection("OpenMeteo"))
            .Validate(settings => { settings.Validate(); return true; })
            .ValidateOnStart();
        services.AddMemoryCache();
        services.AddScoped<IWeatherRepository, WeatherRepository>();
        services.AddHttpClient<IOpenMeteoService, OpenMeteoService>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<OpenMeteoSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UrbanShade/1.0");
        });
        services.AddHostedService<WeatherBackgroundService>();
        var openStreetMap = configuration.GetSection("OpenStreetMap").Get<OpenStreetMapSettings>() ?? new OpenStreetMapSettings();
        openStreetMap.Validate();
        services.AddSingleton(openStreetMap);
        services.AddHttpClient<IOpenStreetMapService, OverpassService>();
        services.AddHostedService<OpenStreetMapBackgroundService>();
        var geo = configuration.GetSection("GoogleEarthEngine").Get<GeoAnalysisSettings>() ?? new GeoAnalysisSettings();
        geo.Validate();
        services.AddSingleton(geo);
        services.AddOptions<GoogleEarthEngineOptions>().Bind(configuration.GetSection("GoogleEarthEngine"));
        services.AddHttpClient<IEarthEngineService, EarthEngineService>(client => client.Timeout = TimeSpan.FromSeconds(90));
        services.AddScoped<IGeoRegionRepository, GeoRegionRepository>();
        services.AddScoped<ISatelliteAnalysisRepository, SatelliteAnalysisRepository>();
        services.AddScoped<IGeoAnalysisJobRepository, GeoAnalysisJobRepository>();
        services.AddScoped<IGreenAreaRepository, GreenAreaRepository>();
        services.AddHostedService<GeoAnalysisBackgroundService>();
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
        services.AddScoped<ICityRepository, CityRepository>();
        services.AddScoped<ISensorReadingRepository, SensorReadingRepository>();
        services.AddScoped<IThermalAnalysisRepository, ThermalAnalysisRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        /// :: Set up sensor simulation settings and validate them.
        var simulation = configuration.GetSection("SensorSimulation").Get<SensorSimulationSettings>() ?? new SensorSimulationSettings();
        simulation.Validate();
        services.AddSingleton(simulation);
        var thermal = configuration.GetSection("ThermalAnalysis").Get<ThermalAnalysisSettings>() ?? new ThermalAnalysisSettings();
        thermal.Validate();
        services.AddSingleton(thermal);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Random.Shared);
        services.AddHostedService<SensorSimulationBackgroundService>();
    }

    private static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<UrbanShadeDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DbConnection");
            options.UseNpgsql(connectionString, postgres => postgres.UseNetTopologySuite());
            options.AddInterceptors(new PersistenceExceptionInterceptor());
        });
    }

}
