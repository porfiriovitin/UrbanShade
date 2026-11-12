using System.Text.Json;
using NetTopologySuite.Geometries;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.RegionThermalAnalysis;
using UrbanShade.Domain.InstitutionalDashboard;
using UrbanShade.Domain.Weather;

namespace UseCases.Tests;

public sealed class ArchitectureRegressionTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);
    private static Polygon Polygon() => new(new LinearRing([new(0,0), new(1,0), new(1,1), new(0,1), new(0,0)])) { SRID = 4326 };

    [Fact]
    public void RegionAndJob_DoNotExposeTheirMutableBoundary()
    {
        var original = Polygon();
        var region = new GeoRegion(Guid.NewGuid(), "Region", null, original);
        original.SRID = 0;
        var exposed = region.Boundary;
        exposed.SRID = 0;
        var job = new GeoAnalysisJob(region, Now.AddDays(-1), Now, "v1", "{}");
        job.Boundary.SRID = 0;
        Assert.Equal(4326, region.Boundary.SRID);
        Assert.Equal(4326, job.Boundary.SRID);
        Assert.Equal(1, region.BoundaryVersion);
        region.Update("Renamed", null, Polygon());
        Assert.Equal(1, region.BoundaryVersion);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    public void InvalidSensorUpdate_IsRejectedBeforeMutation(double latitude, double longitude)
    {
        var sensor = new Sensor("CODE", "Original", null, 0, 0, null);
        Assert.Throws<ArgumentOutOfRangeException>(() => sensor.Update("OTHER", "Changed", null, latitude, longitude, null));
        Assert.Equal("Original", sensor.Name);
        Assert.Equal("CODE", sensor.Code);
    }

    [Fact]
    public void IdentityAndOrganization_RejectInvalidState()
    {
        Assert.Throws<ArgumentException>(() => new User("Valid", "test@example.com", ""));
        var user = new User("Valid", "test@example.com", "hash");
        Assert.Throws<ArgumentException>(() => user.UpdatePassword(""));
        Assert.Equal("hash", user.HashedPassword);
        Assert.Equal(0, user.SecurityVersion);
        var organization = new Organization("Valid", OrganizationType.Municipality, "123", "test@example.com", null);
        Assert.Throws<ArgumentException>(() => organization.Update("Changed", (OrganizationType)999, "123", "test@example.com", null, true));
        Assert.Equal("Valid", organization.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Provenance_ReadsLegacyAndVersionOne_AndRejectsExpiration(int schemaVersion)
    {
        var source = Provenance() with { SchemaVersion = schemaVersion };
        var read = AnalysisProvenance.Read(JsonSerializer.Serialize(source));
        var settings = new RegionThermalAnalysisSettings();
        Assert.True(AnalysisEligibility.Evaluate(read, settings.AlgorithmVersion, Now, Now, settings).IsCurrent);
        Assert.False(AnalysisEligibility.Evaluate(read, settings.AlgorithmVersion, Now, Now.AddMinutes(14), settings).IsCurrent);
        Assert.False(AnalysisEligibility.Evaluate(read, "old", Now, Now, settings).IsCurrent);
        Assert.False(AnalysisEligibility.Evaluate(read, settings.AlgorithmVersion, Now, Now, settings, 1).IsCurrent);
    }

    [Fact]
    public void Provenance_FutureUnsupportedAndChangedPolicyAreNotCurrent()
    {
        var settings = new RegionThermalAnalysisSettings();
        var source = Provenance();
        Assert.Null(AnalysisProvenance.Read(JsonSerializer.Serialize(source with { SchemaVersion = 99 })));
        Assert.False(AnalysisEligibility.Evaluate(source with { Sensors = [new() { MeasuredAt = Now.AddMinutes(1) }] }, "1.0", Now, Now, settings).IsCurrent);
        settings.Weights.Thermal = .6m;
        Assert.False(AnalysisEligibility.Evaluate(source, "1.0", Now, Now, settings).IsCurrent);
        Assert.Null(AnalysisProvenance.Read("invalid"));
        Assert.Null(AnalysisProvenance.Read("{\"sensors\":null}"));
    }

    [Theory]
    [InlineData(false, "CRITICAL", 10, "UNDETERMINED")]
    [InlineData(true, "CRITICAL", 10, "HIGH")]
    [InlineData(true, "HIGH", 90, "MEDIUM")]
    [InlineData(true, "LOW", 90, "LOW")]
    public void InterventionPriority_IsIndependentOfTransport(bool current, string level, decimal vegetation, string expected) =>
        Assert.Equal(expected, InterventionPriority.Calculate(current, level, vegetation, null, false, new()));

    [Fact]
    public void WeatherEntities_PreserveActualProvider()
    {
        var observation = new WeatherObservation(Now, Now, new(30,60,null,null,null,null,null), []) { Source = "OTHER_PROVIDER" };
        var snapshot = new WeatherSnapshot(Guid.NewGuid(), 1, 0, 0, observation);
        var forecast = new WeatherForecast(snapshot.GeoRegionId, 1, 0, 0, new(Now.AddHours(1),30,60,null,null,null,null,null,null), Now, observation.Source);
        Assert.Equal("OTHER_PROVIDER", snapshot.Source);
        Assert.Equal(snapshot.Source, forecast.Source);
    }

    private static AnalysisProvenance Provenance() => new()
    {
        Sensors = [new() { MeasuredAt = Now.AddMinutes(-1) }],
        Vegetation = new() { ObservationEndAt = Now.AddDays(-1) },
        Scores = new() { ["Thermal"] = 50, ["Vegetation"] = 50 },
        AppliedWeights = new() { ["Thermal"] = .7m, ["Vegetation"] = .3m },
        Parameters = new()
    };
}
