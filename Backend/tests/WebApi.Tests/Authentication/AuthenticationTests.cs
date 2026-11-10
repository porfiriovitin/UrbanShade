using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CommomTestsUtilities.Requests;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using UrbanShade.Domain.Entities;
using UrbanShade.Exceptions;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Authentication;

namespace WebApi.Tests.Authentication;

public sealed class AuthenticationTests : BaseIntegrationTest
{
    public AuthenticationTests(UrbanShadeApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_PersistsTextRoleAndHashes_AndAuthenticatesBearer()
    {
        var request = RegisterRequestBuilder.Build();
        var result = await Client.PostAsJsonAsync("/api/auth/register", new { request.Name, Email = request.Email.ToUpperInvariant(), request.Password, Role = "PlatformAdministrator" });
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var data = (await result.Content.ReadFromJsonAsync<PayloadResponse<AuthenticationResponse>>())!.Data!;
        var jwt = new JsonWebToken(data.AccessToken);
        Assert.Empty(jwt.Issuer);
        Assert.Empty(jwt.Audiences);
        Assert.Equal(data.User.Id.ToString(), jwt.Subject);
        Assert.Equal("Citizen", jwt.GetClaim("role").Value);
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT u.\"Role\", pg_typeof(u.\"Role\")::text, u.\"HashedPassword\", t.\"Token\", u.\"Email\" FROM \"Users\" u JOIN \"RefreshTokens\" t ON t.\"UserId\" = u.\"Id\" WHERE u.\"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", data.User.Id);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Citizen", reader.GetString(0));
        Assert.Equal("text", reader.GetString(1));
        Assert.Equal(48, Convert.FromBase64String(reader.GetString(2)).Length);
        Assert.NotEqual(request.Password, reader.GetString(2));
        Assert.NotEqual(data.RefreshToken, reader.GetString(3));
        Assert.Equal(64, reader.GetString(3).Length);
        Assert.Equal(request.Email, reader.GetString(4));
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.AccessToken);
        var me = await Client.GetFromJsonAsync<PayloadResponse<UserResponse>>("/api/auth/me");
        Assert.Equal(data.User.Id, me!.Data!.Id);
    }

    [Fact]
    public async Task Login_NormalizesEmail_RejectsWrongPasswordAndUnknownUser()
    {
        var request = RegisterRequestBuilder.Build();
        (await Client.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();
        var valid = await Client.PostAsJsonAsync("/api/auth/login", new { Email = " " + request.Email.ToUpperInvariant() + " ", request.Password });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var wrong = await Client.PostAsJsonAsync("/api/auth/login", new { request.Email, Password = "wrong-password" });
        var unknown = await Client.PostAsJsonAsync("/api/auth/login", new { Email = "missing@example.com", request.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DuplicateEmail_ReturnsConflict_EvenForConcurrentRegistrations()
    {
        var request = RegisterRequestBuilder.Build();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Client.PostAsJsonAsync("/api/auth/register", request)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveChanges_TranslatesDuplicateEmail(bool synchronous)
    {
        var request = RegisterRequestBuilder.Build();
        (await Client.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        context.Users.Add(new User(request.Name, request.Email, "hash"));

        var exception = synchronous
            ? Assert.Throws<EmailAlreadyRegisteredException>(() => context.SaveChanges())
            : await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() => context.SaveChangesAsync());

        Assert.Equal("Email already registered.", exception.Message);
        var updateException = Assert.IsType<DbUpdateException>(exception.InnerException);
        var postgresException = Assert.IsType<PostgresException>(updateException.InnerException);
        Assert.Equal("IX_Users_Email", postgresException.ConstraintName);
    }

    [Fact]
    public async Task Refresh_Rotates_AndReplayRevokesDescendants()
    {
        var original = await Register();
        var response = await Refresh(original.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var next = (await response.Content.ReadFromJsonAsync<PayloadResponse<AuthenticationResponse>>())!.Data!;
        Assert.NotEqual(original.RefreshToken, next.RefreshToken);
        Assert.NotEqual(original.AccessToken, next.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(original.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(next.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRefresh_OnlyOneSucceeds_AndReplayRevokesFamily()
    {
        var original = await Register();
        var responses = await Task.WhenAll(Refresh(original.RefreshToken), Refresh(original.RefreshToken));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);
        var next = (await responses.Single(r => r.IsSuccessStatusCode).Content.ReadFromJsonAsync<PayloadResponse<AuthenticationResponse>>())!.Data!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(next.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesSession_AndIsIdempotent()
    {
        var original = await Register();
        var response = await Refresh(original.RefreshToken);
        var next = (await response.Content.ReadFromJsonAsync<PayloadResponse<AuthenticationResponse>>())!.Data!;
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync("/api/auth/logout", new { original.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(next.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task ExpiredRefresh_IsRejected()
    {
        var data = await Register();
        await ExecuteSql("UPDATE \"RefreshTokens\" SET \"ExpiresAt\" = NOW() - INTERVAL '1 day' WHERE \"UserId\" = @id", data.User.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(data.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task InactiveUser_CannotLoginRefreshOrGetProfile()
    {
        var request = RegisterRequestBuilder.Build();
        var response = await Client.PostAsJsonAsync("/api/auth/register", request);
        var data = (await response.Content.ReadFromJsonAsync<PayloadResponse<AuthenticationResponse>>())!.Data!;
        await ExecuteSql("UPDATE \"Users\" SET \"Active\" = false WHERE \"Id\" = @id", data.User.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/login", new { request.Email, request.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(data.RefreshToken)).StatusCode);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", data.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Refresh_UsesCurrentRole()
    {
        var data = await Register();
        await ExecuteSql("UPDATE \"Users\" SET \"Role\" = 'Manager' WHERE \"Id\" = @id", data.User.Id);
        var result = (await (await Refresh(data.RefreshToken)).Content.ReadFromJsonAsync<PayloadResponse<AuthenticationResponse>>())!.Data!;
        Assert.Equal("Manager", new JsonWebToken(result.AccessToken).GetClaim("role").Value);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("signature")]
    public async Task Bearer_RejectsInvalidTokens(string scenario)
    {
        if (scenario != "missing")
        {
            var descriptor = new SecurityTokenDescriptor
            {
                IssuedAt = DateTime.UtcNow.AddHours(-2), NotBefore = DateTime.UtcNow.AddHours(-2),
                Expires = scenario == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1),
                Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString(), ["role"] = "Citizen" },
                SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(scenario == "signature" ? new string('x', 64) : UrbanShadeApplicationFactory.SigningKey)), SecurityAlgorithms.HmacSha256)
            };
            Client.DefaultRequestHeaders.Authorization = new("Bearer", new JsonWebTokenHandler().CreateToken(descriptor));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task InvalidRequests_UsePayloadEnvelope()
    {
        var result = await Client.PostAsJsonAsync("/api/auth/register", new { Name = "", Email = "invalid", Password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("Error", (await result.Content.ReadFromJsonAsync<PayloadResponse>())!.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh("invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(new string('a', 86))).StatusCode);
    }

    [Theory]
    [InlineData("Citizen", HttpStatusCode.Forbidden)]
    [InlineData("Manager", HttpStatusCode.Forbidden)]
    [InlineData("PlatformAdministrator", HttpStatusCode.OK)]
    public async Task Administration_RequiresPlatformAdministrator(string role, HttpStatusCode expected)
    {
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString(), ["role"] = role },
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(UrbanShadeApplicationFactory.SigningKey)), SecurityAlgorithms.HmacSha256)
        });
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await Client.GetAsync("/test/administration");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Forbidden)
            Assert.Equal("Error", (await response.Content.ReadFromJsonAsync<PayloadResponse>())!.Status);
    }

    [Fact]
    public async Task Migration_ConvertsExistingRoles_AndInvalidatesLegacyTokens()
    {
        var database = "migration_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(Factory.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin);
            await create.ExecuteNonQueryAsync();
        }
        var connectionString = new NpgsqlConnectionStringBuilder(Factory.ConnectionString) { Database = database }.ConnectionString;
        await using var context = new UrbanShadeDbContext(new DbContextOptionsBuilder<UrbanShadeDbContext>().UseNpgsql(connectionString).Options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261106002431_InitialCreate");
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            for (var role = 0; role < 3; role++)
            {
                await using var insert = new NpgsqlCommand("""
                    INSERT INTO "Users" ("Id", "Name", "Email", "HashedPassword", "Role", "Active", "CreatedAt")
                    VALUES (@id, 'Legacy', @email, 'hash', @role, true, NOW());
                    INSERT INTO "RefreshTokens" ("Id", "Token", "ExpiresAt", "UserId") VALUES (@tokenId, 'legacy-token', NOW(), @id);
                    """, connection);
                insert.Parameters.AddWithValue("id", Guid.NewGuid());
                insert.Parameters.AddWithValue("tokenId", Guid.NewGuid());
                insert.Parameters.AddWithValue("email", $"legacy{role}@example.com");
                insert.Parameters.AddWithValue("role", role);
                await insert.ExecuteNonQueryAsync();
            }
        }
        await migrator.MigrateAsync();
        var roles = await context.Users.OrderBy(u => u.Email).Select(u => u.Role).ToListAsync();
        Assert.Equal(new[] { UrbanShade.Domain.Enums.UserRoles.PlatformAdministrator, UrbanShade.Domain.Enums.UserRoles.Citizen, UrbanShade.Domain.Enums.UserRoles.Manager }, roles);
        Assert.Empty(await context.RefreshTokens.ToListAsync());
        await migrator.MigrateAsync("20261106002431_InitialCreate");
        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();
        await using var query = new NpgsqlCommand("SELECT SUM(\"Role\") FROM \"Users\"", verify);
        Assert.Equal(3L, await query.ExecuteScalarAsync());
    }

    private async Task ExecuteSql(string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }
}
