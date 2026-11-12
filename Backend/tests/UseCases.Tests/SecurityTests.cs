using UrbanShade.Infraestructure.Security;

namespace UseCases.Tests;

public sealed class SecurityTests
{
    [Fact]
    public async Task Argon2id_UsesRandomSalt_AndVerifiesPassword()
    {
        var hasher = new Argon2idPasswordHasher();
        var first = await hasher.Hash("Strong-password-123!");
        var second = await hasher.Hash("Strong-password-123!");
        Assert.NotEqual(first, second);
        Assert.Equal(48, Convert.FromBase64String(first).Length);
        Assert.True(await hasher.Verify("Strong-password-123!", first));
        Assert.False(await hasher.Verify("wrong", first));
        Assert.False(await hasher.Verify("password", "malformed"));
        Assert.False(await hasher.Verify("password", Convert.ToBase64String(new byte[47])));
        Assert.False(await hasher.Verify("password", Convert.ToBase64String(new byte[49])));
    }

    [Fact]
    public void Settings_RejectMissingSigningKey() => Assert.Throws<InvalidOperationException>(() => new JwtSettings().Validate());

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    public void Settings_RejectShortSigningKey(int length) =>
        Assert.Throws<InvalidOperationException>(() => new JwtSettings { SigningKey = new string('k', length) }.Validate());

    [Fact]
    public void Settings_ProvideDefaultIssuerAndAudience() =>
        new JwtSettings { SigningKey = new string('k', 32) }.Validate();

    [Theory]
    [InlineData("", "UrbanShade.API")]
    [InlineData("UrbanShade", " ")]
    public void Settings_RejectMissingTrustBoundary(string issuer, string audience) =>
        Assert.Throws<InvalidOperationException>(() => new JwtSettings { SigningKey = new string('k', 32), Issuer = issuer, Audience = audience }.Validate());
}
