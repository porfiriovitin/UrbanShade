using UrbanShade.Application.UseCases.Organizations;
using UrbanShade.Application.UseCases.Organizations.Members;
using UrbanShade.Application.UseCases.Authentication.ChangePassword;
using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Communication.Requests.Authentication;

namespace Validator.Tests;

public sealed class OrganizationValidatorTests
{
    [Theory]
    [InlineData("Municipality", true)]
    [InlineData("Condominium", true)]
    [InlineData("PublicAgency", true)]
    [InlineData("Company", true)]
    [InlineData("Other", true)]
    [InlineData("0", false)]
    [InlineData("municipality", false)]
    [InlineData("", false)]
    public void Organization_RequiresDefinedType(string type, bool valid)
    {
        Assert.Equal(valid, new OrganizationValidator().Validate(new OrganizationRequest
        {
            Name = "Organization", Type = type, Document = "DOCUMENT", ContactEmail = "contact@example.com"
        }).IsValid);
    }

    [Fact]
    public void Organization_ValidatesRequiredFieldsAndLengths()
    {
        Assert.Equal(4, new OrganizationValidator().Validate(new OrganizationRequest()).Errors.Count);
        Assert.False(new OrganizationValidator().Validate(new OrganizationRequest
        {
            Name = new string('a', 151), Type = "Company", Document = new string('1', 31), ContactEmail = "invalid", ContactPhone = new string('1', 31)
        }).IsValid);
    }

    [Theory]
    [InlineData(" NAME@example.com ", true)]
    [InlineData("invalid", false)]
    [InlineData("Name <name@example.com>", false)]
    public void Member_ValidatesEmail(string email, bool valid)
    {
        Assert.Equal(valid, new OrganizationMemberValidator().Validate(new AddOrganizationMemberRequest { Name = "Manager", Email = email }).IsValid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("short", false)]
    [InlineData("Strong-password-123!", true)]
    public void Password_ValidatesNewPassword(string password, bool valid)
    {
        Assert.Equal(valid, new ChangePasswordValidator(true).Validate(new ChangePasswordRequest { NewPassword = password }).IsValid);
    }

    [Fact]
    public void Password_RequiresCurrentPasswordOnlyOutsideFirstAccess()
    {
        var request = new ChangePasswordRequest { NewPassword = "Strong-password-123!" };
        Assert.True(new ChangePasswordValidator(true).Validate(request).IsValid);
        Assert.False(new ChangePasswordValidator().Validate(request).IsValid);
        Assert.False(new ChangePasswordValidator(true).Validate(new ChangePasswordRequest { NewPassword = new string('é', 513) }).IsValid);
    }
}
