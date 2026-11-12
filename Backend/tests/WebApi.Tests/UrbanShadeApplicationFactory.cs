using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.Tests;

public sealed class UrbanShadeApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "integration-tests-signing-key-64-bytes-long-do-not-use-in-production";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgis/postgis:16-3.7").Build();
    public string ConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Tests");
        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(Resources.AdministrationProbeController).Assembly);
            services.AddSingleton<Weather.WeatherTestProvider>();
            services.AddSingleton<UrbanShade.Domain.Routing.IRoutingProvider, Routes.RouteTestProvider>();
            services.AddSingleton<UrbanShade.Domain.Weather.IOpenMeteoService>(provider => provider.GetRequiredService<Weather.WeatherTestProvider>());
        });
        builder.UseSetting("ConnectionStrings:DbConnection", ConnectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("SensorSimulation:Enabled", "false");
        builder.UseSetting("ThermalAnalysis:Enabled", "false");
        builder.UseSetting("GoogleEarthEngine:Enabled", "false");
        builder.UseSetting("OpenStreetMap:Enabled", "false");
        builder.UseSetting("OpenMeteo:AutomaticRefreshEnabled", "false");
        builder.UseSetting("RegionThermalAnalysis:AutomaticRefreshEnabled", "false");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DbConnection"] = ConnectionString,
            ["Jwt:SigningKey"] = SigningKey,
            // Most integration tests share a loopback address; throttling has dedicated tests.
            ["SecurityTraffic:RequestsPerMinute"] = "100000",
            ["SecurityTraffic:AuthenticationRequestsPerMinute"] = "100000",
            ["SecurityTraffic:AttemptsPerAccountPerMinute"] = "100000",
            ["SensorSimulation:Enabled"] = "false",
            ["ThermalAnalysis:Enabled"] = "false",
            ["OpenMeteo:AutomaticRefreshEnabled"] = "false",
            ["RegionThermalAnalysis:AutomaticRefreshEnabled"] = "false",
            ["OpenRouteService:Api_Key"] = "integration-test-placeholder"
        }));
    }

    public Task InitializeAsync() => _postgres.StartAsync();
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
