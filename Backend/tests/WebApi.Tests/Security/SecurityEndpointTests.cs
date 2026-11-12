using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;
using UrbanShade.Infraestructure.DataAcess;
using Npgsql;

namespace WebApi.Tests.Security;

public sealed class SecurityEndpointTests(UrbanShadeApplicationFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Authentication_EnforcesAccountAndAddressBudgetsBeforeCredentialWork()
    {
        using var limited = Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecurityTraffic:AuthenticationRequestsPerMinute"] = "4",
                ["SecurityTraffic:AttemptsPerAccountPerMinute"] = "2"
            })));
        using var client = limited.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var first = new { Email = "missing@example.test", Password = "Wrong-password-123!" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", first)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { Email = "other@example.test", first.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new { Email = "third@example.test", first.Password })).StatusCode);
    }

    [Fact]
    public async Task Map_RejectsMoreThan1000SensorsInsteadOfMaterializingEntireCatalog()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var prefix = Guid.NewGuid().ToString("N");
        context.Sensors.AddRange(Enumerable.Range(0, 1001).Select(i => new Sensor(prefix + i, "Map sensor", null, 0, 0, null)));
        await context.SaveChangesAsync();
        var repository = scope.ServiceProvider.GetRequiredService<ISensorRepository>();
        await Assert.ThrowsAsync<SensorException>(() => repository.GetActiveForThermalMap(null, null, default));
        var session = await Register();
        Client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/thermal/map")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/sensors/latest-readings")).StatusCode);
    }

    [Fact]
    public async Task ProductionGuard_RejectsTheDatabaseAdministrativeIdentity()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => RuntimeDatabaseSecurity.Validate(Factory.Services));
    }

    [Fact]
    public async Task ProductionGuard_AcceptsDataOnlyIdentity_WhichCannotCreateTables()
    {
        // Both identifiers contain only a fixed prefix and generated hexadecimal characters.
        var role = "runtime_" + Guid.NewGuid().ToString("N");
        var password = Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(Factory.ConnectionString))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"""
                CREATE ROLE {role} LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
                GRANT USAGE ON SCHEMA public TO {role};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {role};
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {role};
                """, admin);
            await command.ExecuteNonQueryAsync();
        }
        var connectionString = new NpgsqlConnectionStringBuilder(Factory.ConnectionString) { Username = role, Password = password }.ConnectionString;
        using var services = new ServiceCollection().AddDbContext<UrbanShadeDbContext>(options =>
            options.UseNpgsql(connectionString, postgres => postgres.UseNetTopologySuite())).BuildServiceProvider();
        await RuntimeDatabaseSecurity.Validate(services);
        await using var runtime = new NpgsqlConnection(connectionString);
        await runtime.OpenAsync();
        await using var create = new NpgsqlCommand("CREATE TABLE public.forbidden_runtime_table (id integer)", runtime);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => create.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }
}
