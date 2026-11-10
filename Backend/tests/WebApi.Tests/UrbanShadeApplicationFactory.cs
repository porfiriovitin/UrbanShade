using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.Tests;

public sealed class UrbanShadeApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "integration-tests-signing-key-64-bytes-long-do-not-use-in-production";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    public string ConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Tests");
        builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(Resources.AdministrationProbeController).Assembly));
        builder.UseSetting("ConnectionStrings:DbConnection", ConnectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DbConnection"] = ConnectionString,
            ["Jwt:SigningKey"] = SigningKey
        }));
    }

    public Task InitializeAsync() => _postgres.StartAsync();
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
