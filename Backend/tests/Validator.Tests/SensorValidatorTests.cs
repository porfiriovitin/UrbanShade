using System.Text.Json;
using UrbanShade.Application.UseCases.Sensors.Register;
using UrbanShade.Application.UseCases.Sensors.Update;
using UrbanShade.Application.UseCases.Sensors.UpdateStatus;
using UrbanShade.Application.UseCases.Sensors.GetAll;
using UrbanShade.Communication.Requests.Sensors;

namespace Validator.Tests;

public sealed class SensorValidatorTests
{
    [Theory]
    [InlineData(-90, -180, true)]
    [InlineData(90, 180, true)]
    [InlineData(-90.1, 0, false)]
    [InlineData(90.1, 0, false)]
    [InlineData(0, -180.1, false)]
    [InlineData(0, 180.1, false)]
    [InlineData(double.NaN, 0, false)]
    [InlineData(0, double.PositiveInfinity, false)]
    public void Register_ValidatesCoordinates(double latitude, double longitude, bool valid)
    {
        var request = new RegisterSensorRequest { Code = "URB-001", Name = "Sensor", Latitude = latitude, Longitude = longitude };
        Assert.Equal(valid, new RegisterSensorValidator().Validate(request).IsValid);
    }

    [Fact]
    public void Register_RequiresCodeNameAndCoordinates()
    {
        var result = new RegisterSensorValidator().Validate(new RegisterSensorRequest());
        Assert.Equal(new[] { "Code", "Name", "Latitude", "Longitude" }, result.Errors.Select(error => error.PropertyName));
    }

    [Theory]
    [InlineData(50, 150, 500, true)]
    [InlineData(51, 150, 500, false)]
    [InlineData(50, 151, 500, false)]
    [InlineData(50, 150, 501, false)]
    public void Register_ValidatesTrimmedLengths(int code, int name, int description, bool valid)
    {
        var request = new RegisterSensorRequest
        {
            Code = " " + new string('a', code) + " ", Name = new string('a', name),
            Description = new string('a', description), Latitude = 0, Longitude = 0
        };
        Assert.Equal(valid, new RegisterSensorValidator().Validate(request).IsValid);
    }

    [Theory]
    [InlineData("{}" , true)]
    [InlineData("{\"name\":null}", false)]
    [InlineData("{\"code\":\" \"}", false)]
    [InlineData("{\"latitude\":null}", false)]
    [InlineData("{\"longitude\":181}", false)]
    [InlineData("{\"description\":null,\"installedAt\":null}", true)]
    public void Update_ValidatesOnlySuppliedFields(string json, bool valid)
    {
        var request = JsonSerializer.Deserialize<UpdateSensorRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(valid, new UpdateSensorValidator().Validate(request).IsValid);
    }

    [Theory]
    [InlineData("ACTIVE", true)]
    [InlineData("INACTIVE", true)]
    [InlineData("MAINTENANCE", true)]
    [InlineData("active", false)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Status_RequiresNamedValue(string? status, bool valid)
    {
        Assert.Equal(valid, new UpdateSensorStatusValidator().Validate(new UpdateSensorStatusRequest { Status = status! }).IsValid);
        Assert.Equal(status is null || valid, new GetSensorsValidator().Validate(new GetSensorsRequest { Status = status }).IsValid);
    }

    [Fact]
    public void InstalledAt_RejectsUnspecifiedTime()
    {
        Assert.False(new RegisterSensorValidator().Validate(new RegisterSensorRequest
        {
            Code = "URB-001", Name = "Sensor", Latitude = 0, Longitude = 0, InstalledAt = new DateTime(2026, 11, 10)
        }).IsValid);
    }
}
