using Microsoft.Extensions.Configuration;
using System.Text.Json;
using UrbanShade.API.Authentication;
using UrbanShade.Application.UseCases.InstitutionalDashboard;
using UrbanShade.Infraestructure.DataAcess;

namespace WebApi.Tests.Security;

// These tests exercise security primitives without a database or external services.
public sealed class SecurityControlTests
{
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["SecurityTraffic:ConcurrentRequests"] = "1",
        ["SecurityTraffic:RequestsPerMinute"] = "2",
        ["SecurityTraffic:AttemptsPerAccountPerMinute"] = "2"
    }).Build();

    [Fact]
    public void Admission_BoundsConcurrentWork_AndReleasesCapacity()
    {
        using var admission = new RequestAdmission(Configuration());
        using (var first = admission.Enter())
        {
            Assert.True(first.IsAcquired);
            using var rejected = admission.Enter();
            Assert.False(rejected.IsAcquired);
        }
        using var accepted = admission.Enter();
        Assert.True(accepted.IsAcquired);
    }

    [Fact]
    public void Admission_BoundsAddressesIndependently()
    {
        using var admission = new RequestAdmission(Configuration());
        using var first = admission.Address("127.0.0.1");
        using var second = admission.Address("127.0.0.1");
        using var rejected = admission.Address("127.0.0.1");
        using var other = admission.Address("127.0.0.2");
        Assert.True(first.IsAcquired && second.IsAcquired && other.IsAcquired);
        Assert.False(rejected.IsAcquired);
    }

    [Fact]
    public void Admission_CombinesNormalizedAccountAttemptsAcrossAddresses()
    {
        using var admission = new RequestAdmission(Configuration());
        using var first = admission.Account(" Test@example.test ");
        using var second = admission.Account("test@example.test");
        using var rejected = admission.Account("TEST@example.test");
        Assert.True(first.IsAcquired && second.IsAcquired);
        Assert.False(rejected.IsAcquired);
    }

    [Fact]
    public void PublicMetadata_RemovesIdentifiersFromHistoricalSensors()
    {
        var json = JsonSerializer.Serialize(new { sensors = new[] { new { sensorId = Guid.NewGuid(), readingId = Guid.NewGuid(), analysisId = Guid.NewGuid(), MeasuredAt = DateTime.UtcNow, AlgorithmVersion = "1.0", IsSimulated = true } } });
        var metadata = InstitutionalDashboardQuery.PublicMetadata(json);
        Assert.False(metadata.TryGetProperty("sensors", out _));
        var observation = Assert.Single(metadata.GetProperty("sensorObservations").EnumerateArray());
        Assert.False(observation.TryGetProperty("sensorId", out _));
        Assert.False(observation.TryGetProperty("readingId", out _));
        Assert.False(observation.TryGetProperty("analysisId", out _));
        Assert.True(observation.GetProperty("IsSimulated").GetBoolean());
    }

    [Fact]
    public void InstitutionalCache_RejectsOversizedEntries_AndBoundsTotalRetention()
    {
        using var cache = new InstitutionalDashboardCache();
        var expires = DateTimeOffset.UtcNow.AddMinutes(5);
        Assert.False(cache.Set("large", new string('x', (int)InstitutionalDashboardCache.MaximumEntrySize), expires));
        Assert.False(cache.TryGetValue<string>("large", out _));
        for (var i = 0; i < 1000; i++) cache.Set(i.ToString(), new string('x', 16000), expires);
        var retained = Enumerable.Range(0, 1000).Count(i => cache.TryGetValue<string>(i.ToString(), out _));
        Assert.InRange(retained, 0, 300);
    }
}
