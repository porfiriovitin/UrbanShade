using System.Text.Json;
using NetTopologySuite.Geometries;
using UrbanShade.Application.UseCases.Routes.Compare;
using UrbanShade.Communication.Requests.Routes;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.RegionThermalAnalysis;
using UrbanShade.Domain.Routing;

namespace UseCases.Tests.Routes;

public sealed class RouteCalculatorTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 15, 0, 0, DateTimeKind.Utc);
    private readonly RoutingSettings _settings = new();

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void Validator_RejectsInvalidCoordinates(double latitude, double longitude)
    {
        Assert.False(new CompareRoutesValidator(_settings).Validate(Request(latitude, longitude)).IsValid);
    }

    [Fact]
    public void Validator_RejectsNullPointsCoincidenceUnsupportedModeAndExcessDistance()
    {
        var validator = new CompareRoutesValidator(_settings);
        Assert.False(validator.Validate(new CompareRoutesRequest()).IsValid);
        Assert.False(validator.Validate(Request(0, .01)).IsValid);
        var request = Request(0, 0);
        request.TravelMode = "DRIVING";
        Assert.False(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(Request(30, 30)).IsValid);
        Assert.True(validator.Validate(Request(0, 0)).IsValid);
    }

    [Fact]
    public void Exposure_WeightsOnlyKnownDistance_AndReportsUnknownCoverage()
    {
        var result = Calculator().Calculate(Route(), [Segment(200, 40), Segment(300, 70), Segment(500, 80), Segment(250, null)], Now);
        Assert.Equal(69, result.ExposureIndex);
        Assert.Equal(80, result.ThermalCoveragePercent);
        Assert.Equal(800, result.HighExposureDistanceMeters);
        Assert.Equal(64, result.HighExposurePercent);
        Assert.Equal("PARTIAL", result.DataQuality);
        Assert.Equal(1, result.HighExposureSegmentCount);
        Assert.True(result.CanRecommendThermally);
    }

    [Fact]
    public void NoKnownSegments_DoesNotInventZeroExposure()
    {
        var result = Calculator().Calculate(Route(), [Segment(1000, null)], Now);
        Assert.Null(result.ExposureIndex);
        Assert.Null(result.ExposureLevel);
        Assert.Equal(0, result.ThermalCoveragePercent);
        Assert.False(result.CanRecommendThermally);
    }

    [Theory]
    [InlineData(16, 1)]
    [InlineData(1, 91)]
    public void ExpiredSources_AreUnknown_EvenWhenAnalysisIsRecent(int minutes, int days)
    {
        var segment = Segment(1000, 50) with { SourceMetadata = Metadata(Now.AddMinutes(-minutes), Now.AddDays(-days)) };
        Assert.Null(Calculator().Calculate(Route(), [segment], Now).ExposureIndex);
    }

    [Fact]
    public void CorruptMetadataDifferentAlgorithmAndLowConfidence_CannotDriveRecommendations()
    {
        Assert.Null(Calculator().Calculate(Route(), [Segment(1000, 50) with { SourceMetadata = "{}" }], Now).ExposureIndex);
        Assert.Null(Calculator().Calculate(Route(), [Segment(1000, 50) with { AlgorithmVersion = "old" }], Now).ExposureIndex);
        var low = Calculator().Calculate(Route(), [Segment(1000, 50) with { ConfidenceLevel = ConfidenceLevel.LOW }], Now);
        Assert.NotNull(low.ExposureIndex);
        Assert.False(low.CanRecommendThermally);
    }

    [Theory]
    [InlineData(65, 1250, 1200, "LOWER_HEAT_EXPOSURE")]
    [InlineData(66, 1100, 800, "NO_BETTER_ALTERNATIVE")]
    [InlineData(40, 1251, 800, "NO_BETTER_ALTERNATIVE")]
    [InlineData(40, 1100, 1201, "NO_BETTER_ALTERNATIVE")]
    public void Recommendation_RespectsBenefitDistanceAndTimeLimits(int index, double distance, double duration, string expected)
    {
        var conventional = Calculator().Calculate(Route(), [Segment(1000, 70)], Now);
        var alternative = Calculator().Calculate(Route("alternative", false, distance, duration), [Segment(distance, index)], Now);
        Assert.Equal(expected, new RouteRecommendationCalculator(_settings).Recommend([conventional, alternative]).Reason);
    }

    [Fact]
    public void WorseCoverageAndInsufficientReference_KeepConventionalRoute()
    {
        var conventional = Calculator().Calculate(Route(), [Segment(1000, 70)], Now);
        var alternative = Calculator().Calculate(Route("alternative", false), [Segment(800, 10), Segment(200, null)], Now);
        var recommendation = new RouteRecommendationCalculator(_settings).Recommend([conventional, alternative]);
        Assert.Equal("conventional", recommendation.RouteId);
        Assert.Equal("INSUFFICIENT_THERMAL_DATA", recommendation.Reason);
        Assert.Equal("SINGLE_ROUTE_AVAILABLE", new RouteRecommendationCalculator(_settings).Recommend([conventional]).Reason);
    }

    [Fact]
    public void Settings_RejectUnsafeUrlAndInvalidLimits()
    {
        new RoutingSettings().Validate();
        Assert.Throws<InvalidOperationException>(new RoutingSettings { BaseUrl = "https://example.com/" }.Validate);
        Assert.Throws<InvalidOperationException>(new RoutingSettings { MaxAlternativeRoutes = 4 }.Validate);
        Assert.Throws<InvalidOperationException>(new RoutingSettings { MinimumThermalCoveragePercent = 101 }.Validate);
    }

    private RouteExposureCalculator Calculator() => new(_settings, new RegionThermalAnalysisSettings());
    private static CompareRoutesRequest Request(double latitude, double longitude) => new() { Origin = new() { Latitude = latitude, Longitude = longitude }, Destination = new() { Latitude = 0, Longitude = .01 } };
    internal static RouteCandidate Route(string id = "conventional", bool conventional = true, double distance = 1000, double duration = 720) =>
        new(id, new LineString([new(0,0), new(.01,0)]) { SRID = 4326 }, distance, duration, conventional, "test", "test attribution", []);

    internal static RouteThermalSegment Segment(double distance, decimal? index) => new(0, 1, 0, Route().Geometry, distance,
        Guid.Empty, index.HasValue ? Guid.NewGuid() : null, index, index >= 75 ? ExposureLevel.CRITICAL : index >= 50 ? ExposureLevel.HIGH : ExposureLevel.MODERATE,
        RegionDataQuality.PARTIAL, ConfidenceLevel.MEDIUM, "SIMULATED_SENSORS", "1.0", Now, Metadata(Now.AddMinutes(-1),Now.AddDays(-1)));

    private static string Metadata(DateTime sensorTime, DateTime satelliteTime) => JsonSerializer.Serialize(new
    {
        scores = new { Thermal = 50, Vegetation = 80 }, appliedWeights = new { Thermal = .7142857142857142857142857143m, Vegetation = .2857142857142857142857142857m },
        parameters = new RegionThermalAnalysisSettings(), sensors = new[] { new { MeasuredAt = sensorTime } }, vegetation = new { ObservationEndAt = satelliteTime }
    });
}
