using UrbanShade.Application.UseCases.Authentication.Login.WithEmailAndPassword;
using UrbanShade.Application.UseCases.Authentication.Logout;
using UrbanShade.Application.UseCases.Authentication.Refresh;
using UrbanShade.Application.UseCases.Authentication.Register;
using UrbanShade.Communication.Requests.Authentication;

namespace Validator.Tests;

public sealed class AuthenticationValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("Test <test@example.com>")]
    public void Email_RejectsInvalidValues(string? email)
    {
        Assert.Contains(new RegisterValidator().Validate(Register(email: email!)).Errors, error => error.PropertyName == "Email");
        Assert.Contains(new LoginValidator().Validate(new LoginRequest { Email = email!, Password = "valid" }).Errors,
            error => error.PropertyName == "Email");
    }

    [Fact]
    public void Email_AcceptsSurroundingWhitespaceAndUppercase()
    {
        Assert.True(new RegisterValidator().Validate(Register(email: " TEST@example.com ")).IsValid);
        Assert.True(new LoginValidator().Validate(new LoginRequest { Email = " TEST@example.com ", Password = "valid" }).IsValid);
    }

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void Email_EnforcesLengthLimit(int length, bool valid)
    {
        var email = "user@" + new string('a', length - 9) + ".com";
        Assert.Equal(valid, new RegisterValidator().Validate(Register(email: email)).IsValid);
        Assert.Equal(valid, new LoginValidator().Validate(new LoginRequest { Email = email, Password = "valid" }).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void RegisterPassword_RejectsShortValues(string? password) =>
        Assert.Contains(new RegisterValidator().Validate(Register(password: password!)).Errors,
            error => error.PropertyName == "Password");

    [Fact]
    public void Password_RejectsOversizedUtf8()
    {
        var password = new string('é', 513);
        Assert.False(new RegisterValidator().Validate(Register(password: password)).IsValid);
        Assert.False(new LoginValidator().Validate(new LoginRequest { Email = "test@example.com", Password = password }).IsValid);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(1024)]
    public void RegisterPassword_AcceptsBoundaries(int length) =>
        Assert.True(new RegisterValidator().Validate(Register(password: new string('a', length))).IsValid);

    [Fact]
    public void Password_AcceptsUtf8ByteLimit()
    {
        var password = new string('é', 512);
        Assert.True(new RegisterValidator().Validate(Register(password: password)).IsValid);
        Assert.True(new LoginValidator().Validate(new LoginRequest { Email = "test@example.com", Password = password }).IsValid);
    }

    [Fact]
    public void LoginPassword_DoesNotRequireRegistrationMinimum() =>
        Assert.True(new LoginValidator().Validate(new LoginRequest { Email = "test@example.com", Password = "a" }).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LoginPassword_RejectsMissingValues(string? password) =>
        Assert.Contains(new LoginValidator().Validate(new LoginRequest { Email = "test@example.com", Password = password! }).Errors,
            error => error.PropertyName == "Password");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Name_RejectsBlankValues(string? name) =>
        Assert.Contains(new RegisterValidator().Validate(Register(name: name!)).Errors, error => error.PropertyName == "Name");

    [Theory]
    [InlineData(150, true)]
    [InlineData(151, false)]
    public void Name_EnforcesTrimmedLengthLimit(int length, bool valid) =>
        Assert.Equal(valid, new RegisterValidator().Validate(Register(name: " " + new string('a', length) + " ")).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    public void RefreshToken_RejectsMissingOrShortValues(string? token) => AssertToken(token, false);

    [Theory]
    [InlineData(85)]
    [InlineData(87)]
    public void RefreshToken_RequiresExactLength(int length) => AssertToken(new string('a', length), false);

    [Theory]
    [InlineData('=')]
    [InlineData('+')]
    [InlineData('/')]
    [InlineData('é')]
    [InlineData(' ')]
    public void RefreshToken_RejectsInvalidCharacters(char character) => AssertToken(new string('a', 85) + character, false);

    [Fact]
    public void RefreshToken_AcceptsBase64UrlAlphabet() =>
        AssertToken("AZaz09-_" + new string('a', 78), true);

    [Fact]
    public void Register_CollectsAllInvalidFields()
    {
        var result = new RegisterValidator().Validate(new RegisterRequest());
        Assert.Equal(new[] { "Name", "Email", "Password" }, result.Errors.Select(error => error.PropertyName));
    }

    private static void AssertToken(string? token, bool valid)
    {
        var request = new RefreshTokenRequest { RefreshToken = token! };
        Assert.Equal(valid, new RefreshValidator().Validate(request).IsValid);
        Assert.Equal(valid, new LogoutValidator().Validate(request).IsValid);
    }

    private static RegisterRequest Register(string name = "Test User", string email = "test@example.com",
        string password = "Strong-password-123!") => new() { Name = name, Email = email, Password = password };
}
