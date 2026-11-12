using Microsoft.IdentityModel.JsonWebTokens;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Security;
using UrbanShade.Infraestructure.Security;

namespace UseCases.Tests.Organizations;

public sealed class OrganizationSecurityTests
{
    [Fact]
    public void Token_CarriesInstitutionContextAndPurpose()
    {
        var user = new User("Manager", "manager@example.com", "hash");
        user.UpdateRole(UserRoles.Manager);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var service = new JwtTokenService(new JwtSettings { SigningKey = new string('k', 64) });
        var sessionId = Guid.NewGuid();
        var jwt = new JsonWebToken(service.CreateAccessToken(user, sessionId, ids).Value);
        Assert.Equal(sessionId.ToString(), jwt.GetClaim("sid").Value);
        Assert.Equal("access", jwt.GetClaim("purpose").Value);
        Assert.Equal("0", jwt.GetClaim("security_version").Value);
        Assert.All(ids, id => Assert.Contains(id.ToString(), jwt.Claims.Where(claim => claim.Type == "organization_ids").Select(claim => claim.Value)));
    }

    [Fact]
    public void FirstAccessToken_HasRestrictedRoleAndShortExpiry()
    {
        var user = new User("Manager", "manager@example.com", "hash");
        user.UpdateRole(UserRoles.Manager);
        var expires = DateTime.UtcNow.AddMinutes(2);
        user.SetTemporaryPassword("temporary-hash", expires);
        var service = new JwtTokenService(new JwtSettings { SigningKey = new string('k', 64) });
        var token = service.CreateFirstAccessToken(user);
        var jwt = new JsonWebToken(token.Value);
        Assert.Equal("first_access", jwt.GetClaim("purpose").Value);
        Assert.Equal("FirstAccess", jwt.GetClaim("role").Value);
        Assert.Equal("1", jwt.GetClaim("security_version").Value);
        Assert.True(token.ExpiresAt <= expires);
        user.UpdatePassword("permanent-hash");
        Assert.False(user.MustChangePassword);
        Assert.Null(user.TemporaryPasswordExpiresAt);
        Assert.Equal(2, user.SecurityVersion);
    }

    [Fact]
    public void Sensor_RejectsTransferButAcceptsSameOrganization()
    {
        var sensor = new Sensor("ONE", "Sensor", null, 0, 0, null);
        var id = Guid.NewGuid();
        sensor.AssignOrganization(id);
        sensor.AssignOrganization(id);
        Assert.Equal(id, sensor.OrganizationId);
        Assert.Throws<InvalidOperationException>(() => sensor.AssignOrganization(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(169, 10)]
    [InlineData(24, 0)]
    [InlineData(24, 61)]
    public void FirstAccessSettings_RejectInvalidDurations(int hours, int minutes)
    {
        Assert.Throws<InvalidOperationException>(() => new FirstAccessSettings { TemporaryPasswordHours = hours, TokenMinutes = minutes }.Validate());
    }
}
