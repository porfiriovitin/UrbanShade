using UrbanShade.Application.UseCases.Cities.Register;
using UrbanShade.Application.UseCases.Sensors.Register;
using UrbanShade.Application.UseCases.Sensors.Update;
using UrbanShade.Communication.Requests.Cities;
using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Domain.Entities;

namespace Validator.Tests.Cities;

public sealed class CityValidatorTests
{
    [Theory]
    [InlineData("SP", "3550308", true)]
    [InlineData(" sp ", " 3550308 ", true)]
    [InlineData("RJ", "3304557", true)]
    [InlineData("MG", "3106200", true)]
    [InlineData("DF", "5300108", true)]
    [InlineData("RJ", "3550308", false)]
    [InlineData("XX", "3550308", false)]
    [InlineData("SP", "355030", false)]
    [InlineData("SP", "35503080", false)]
    [InlineData("SP", "35503A8", false)]
    [InlineData("SP", "３５５０３０８", false)]
    [InlineData(null, "3550308", false)]
    [InlineData("SP", null, false)]
    public void Registration_ValidatesBrazilianStateAndIbgeFormat(string? state, string? code, bool valid)
    {
        var request = new RegisterCityRequest { Name = "City", State = state!, IbgeCode = code! };
        Assert.Equal(valid, new RegisterCityValidator().Validate(request).IsValid);
        if (valid) Assert.Equal(code!.Trim(), new City(request.Name, request.State, request.IbgeCode).IbgeCode);
        else Assert.Throws<ArgumentException>(() => new City(request.Name, request.State, request.IbgeCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Registration_RequiresName(string? name)
    {
        Assert.False(new RegisterCityValidator().Validate(new RegisterCityRequest { Name = name!, State = "SP", IbgeCode = "3550308" }).IsValid);
    }

    [Fact]
    public void Registration_EnforcesNameLength_AndSensorRejectsEmptyCityId()
    {
        Assert.False(new RegisterCityValidator().Validate(new RegisterCityRequest { Name = new string('a', 151), State = "SP", IbgeCode = "3550308" }).IsValid);
        Assert.False(new RegisterSensorValidator().Validate(new RegisterSensorRequest { Code = "ONE", Name = "One", Latitude = 0, Longitude = 0, CityId = Guid.Empty }).IsValid);
        Assert.False(new UpdateSensorValidator().Validate(new UpdateSensorRequest { CityId = Guid.Empty }).IsValid);
        Assert.True(new UpdateSensorValidator().Validate(new UpdateSensorRequest { CityId = null }).IsValid);
    }
}
