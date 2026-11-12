using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UrbanShade.Application.UseCases.SensorReadings.Generate;
using UrbanShade.Application.UseCases.ThermalAnalyses.GetLatest;
using UrbanShade.Application.UseCases.ThermalAnalyses.ProcessPending;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.ThermalAnalyses;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Simulation;
using UrbanShade.Domain.ThermalAnalysis;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.DataAcess.Repositories;
using UrbanShade.Infraestructure.Security;

namespace WebApi.Tests.ThermalAnalyses;

public sealed class ThermalAnalysisTests : BaseIntegrationTest
{
    public ThermalAnalysisTests(UrbanShadeApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Processing_PersistsCelsius_IsIdempotent_AndPreservesVersions()
    {
        var sensor = await CreateSensor();
        var reading = await AddReading(sensor.Id, DateTime.UtcNow);
        await Process();
        await Process();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var analysis = Assert.Single(await context.ThermalAnalyses.Where(item => item.SensorReadingId == reading.Id).ToListAsync());
            Assert.Equal(38.51m, analysis.HeatIndex);
            Assert.Equal(ThermalRiskLevel.HIGH, analysis.RiskLevel);
            Assert.Equal(DateTimeKind.Utc, analysis.AnalyzedAt.Kind);
        }
        await Process("2.0");
        Authenticate(UserRoles.Citizen);
        var latest = (await Client.GetFromJsonAsync<PayloadResponse<ThermalAnalysisResponse>>($"/api/sensors/{sensor.Id}/thermal-analysis/latest"))!.Data!;
        Assert.Equal(33.1m, latest.Temperature);
        Assert.Equal(38.51m, latest.HeatIndex);
        Assert.Equal("HIGH", latest.RiskLevel);
        Assert.Equal("1.0", latest.AlgorithmVersion);
        Assert.Equal("CURRENT", latest.DataStatus);
        Assert.Single((await History(sensor.Id)).Items);
        Assert.Equal("2.0", Assert.Single((await History(sensor.Id, "?algorithmVersion=2.0")).Items).AlgorithmVersion);

        await using var verification = Factory.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        Assert.Equal(2, await db.ThermalAnalyses.CountAsync(item => item.SensorReadingId == reading.Id));
        db.ThermalAnalyses.Add(new ThermalAnalysis(reading.Id, 38.51m, ThermalRiskLevel.HIGH, "1.0", DateTime.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentProcessors_InsertOnlyOneAnalysis()
    {
        var sensor = await CreateSensor();
        var reading = await AddReading(sensor.Id, DateTime.UtcNow);
        await using var firstScope = Factory.Services.CreateAsyncScope();
        await using var secondScope = Factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IThermalAnalysisRepository>();
        var second = secondScope.ServiceProvider.GetRequiredService<IThermalAnalysisRepository>();
        var results = await Task.WhenAll(
            first.TryAddForActiveSensor(new ThermalAnalysis(reading.Id, 38.51m, ThermalRiskLevel.HIGH, "1.0", DateTime.UtcNow), default),
            second.TryAddForActiveSensor(new ThermalAnalysis(reading.Id, 38.51m, ThermalRiskLevel.HIGH, "1.0", DateTime.UtcNow), default));
        Assert.Single(results, inserted => inserted);
    }

    [Fact]
    public async Task Processing_RechecksSensorStatus_AndPreservesExistingHistory()
    {
        var sensor = await CreateSensor();
        var analyzed = await AddReading(sensor.Id, DateTime.UtcNow.AddMinutes(-1));
        await Process();
        var pending = await AddReading(sensor.Id, DateTime.UtcNow);
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        (await context.Sensors.SingleAsync(item => item.Id == sensor.Id)).UpdateStatus(SensorStatus.INACTIVE);
        await context.SaveChangesAsync();

        var repository = scope.ServiceProvider.GetRequiredService<IThermalAnalysisRepository>();
        Assert.False(await repository.TryAddForActiveSensor(new ThermalAnalysis(pending.Id, 38.51m, ThermalRiskLevel.HIGH, "1.0", DateTime.UtcNow), default));
        Assert.DoesNotContain(await repository.GetPending("1.0", 100, [], default), item => item.Id == pending.Id);
        Assert.True(await context.ThermalAnalyses.AnyAsync(item => item.SensorReadingId == analyzed.Id));
        Assert.False(await context.ThermalAnalyses.AnyAsync(item => item.SensorReadingId == pending.Id));
    }

    [Fact]
    public async Task History_FiltersDatesRiskAndVersion_WithStablePagination()
    {
        var sensor = await CreateSensor();
        var date = new DateTime(2026, 11, 11, 13, 0, 0, DateTimeKind.Utc);
        var readings = new List<SensorReading>();
        for (var i = 0; i < 4; i++) readings.Add(await AddReading(sensor.Id, date.AddMinutes(i)));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            context.ThermalAnalyses.AddRange(readings.Select((reading, index) => new ThermalAnalysis(reading.Id, index == 1 ? 28 : 38, index == 1 ? ThermalRiskLevel.MODERATE : ThermalRiskLevel.HIGH, "1.0", date.AddHours(-index))));
            await context.SaveChangesAsync();
        }
        Authenticate(UserRoles.PlatformAdministrator);
        var start = Uri.EscapeDataString("2026-11-11T10:00:00-03:00");
        var end = Uri.EscapeDataString("2026-11-11T10:02:00-03:00");
        var query = $"?startDate={start}&endDate={end}&riskLevel=HIGH&pageSize=1";
        var first = await History(sensor.Id, query);
        Assert.Equal(2, first.TotalItems);
        Assert.Equal(date.AddMinutes(2), Assert.Single(first.Items).MeasuredAt);
        var second = await History(sensor.Id, query + "&page=2");
        Assert.Equal(date, Assert.Single(second.Items).MeasuredAt);
        Assert.Empty((await History(sensor.Id, "?algorithmVersion=absent")).Items);

        var latest = (await Client.GetFromJsonAsync<PayloadResponse<ThermalAnalysisResponse>>($"/api/sensors/{sensor.Id}/thermal-analysis/latest"))!.Data!;
        Assert.Equal(date.AddMinutes(3), latest.MeasuredAt);
        Assert.Equal(readings[3].Id, latest.SensorReadingId);
    }

    [Fact]
    public async Task Map_IncludesEmptyAndStaleSensors_AndExcludesInactiveSensors()
    {
        var current = await CreateSensor();
        var stale = await CreateSensor();
        var empty = await CreateSensor();
        var inactive = await CreateSensor(SensorStatus.INACTIVE);
        await AddReading(current.Id, DateTime.UtcNow);
        await AddReading(stale.Id, DateTime.UtcNow.AddMinutes(-20));
        await AddReading(inactive.Id, DateTime.UtcNow);
        await Process();
        Authenticate(UserRoles.Citizen);

        var map = (await Client.GetFromJsonAsync<PayloadResponse<ThermalMapResponse>>("/api/thermal/map"))!.Data!;
        var currentPoint = Assert.Single(map.Sensors, item => item.SensorId == current.Id);
        Assert.Equal("CURRENT", currentPoint.DataStatus);
        Assert.Equal(current.Latitude, currentPoint.Latitude);
        Assert.Equal(current.Longitude, currentPoint.Longitude);
        Assert.Equal(current.Name, currentPoint.SensorName);
        Assert.Equal("STALE", Assert.Single(map.Sensors, item => item.SensorId == stale.Id).DataStatus);
        var emptyPoint = Assert.Single(map.Sensors, item => item.SensorId == empty.Id);
        Assert.Equal("NO_DATA", emptyPoint.DataStatus);
        Assert.Null(emptyPoint.HeatIndex);
        Assert.Null(emptyPoint.RiskLevel);
        Assert.DoesNotContain(map.Sensors, item => item.SensorId == inactive.Id);

        var latestEmpty = (await Client.GetFromJsonAsync<PayloadResponse<ThermalAnalysisResponse>>($"/api/sensors/{empty.Id}/thermal-analysis/latest"))!.Data!;
        Assert.Equal("NO_DATA", latestEmpty.DataStatus);
        Assert.Null(latestEmpty.HeatIndex);
        var overview = (await Client.GetFromJsonAsync<PayloadResponse<ThermalOverviewResponse>>("/api/thermal/overview"))!.Data!;
        Assert.True(overview.TotalActiveSensors >= 3);
        Assert.True(overview.SensorsWithCurrentData >= 1);
        Assert.Equal(38.51m, overview.HighestHeatIndex);
    }

    [Fact]
    public async Task Staleness_UsesMeasurementTime_AndExactConfiguredThreshold()
    {
        var sensor = await CreateSensor();
        var time = new FixedTime();
        var measuredAt = time.GetUtcNow().UtcDateTime.AddMinutes(-15);
        await AddReading(sensor.Id, measuredAt);
        await Process();
        await using var scope = Factory.Services.CreateAsyncScope();
        var settings = new ThermalAnalysisSettings();
        var useCase = new GetLatestThermalAnalysisUseCase(scope.ServiceProvider.GetRequiredService<ISensorRepository>(),
            scope.ServiceProvider.GetRequiredService<IThermalAnalysisRepository>(), settings, time);
        Assert.Equal("CURRENT", (await useCase.Execute(sensor.Id, false)).DataStatus);
        time.Now = time.Now.AddTicks(1);
        Assert.Equal("STALE", (await useCase.Execute(sensor.Id, false)).DataStatus);
        settings.StaleDataThresholdMinutes = 20;
        Assert.Equal("CURRENT", (await useCase.Execute(sensor.Id, false)).DataStatus);
        settings.AlgorithmVersion = "missing";
        var response = await useCase.Execute(sensor.Id, false);
        Assert.Equal("NO_DATA", response.DataStatus);
        Assert.Null(response.HeatIndex);
    }

    [Theory]
    [InlineData(UserRoles.Citizen)]
    [InlineData(UserRoles.Manager)]
    [InlineData(UserRoles.PlatformAdministrator)]
    public async Task Permissions_ProtectHistoryAndLatest(UserRoles role)
    {
        var inactive = await CreateSensor(SensorStatus.INACTIVE);
        var maintenance = await CreateSensor(SensorStatus.MAINTENANCE);
        var deleted = await CreateSensor(deleted: true);
        Authenticate(role);
        var expected = role == UserRoles.PlatformAdministrator ? HttpStatusCode.OK : HttpStatusCode.NotFound;
        foreach (var sensor in new[] { inactive, maintenance, deleted })
            Assert.Equal(expected, (await Client.GetAsync($"/api/sensors/{sensor.Id}/thermal-analyses")).StatusCode);
        foreach (var sensor in new[] { inactive, maintenance })
            Assert.Equal(expected, (await Client.GetAsync($"/api/sensors/{sensor.Id}/thermal-analysis/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/sensors/{deleted.Id}/thermal-analysis/latest")).StatusCode);
    }

    [Fact]
    public async Task AuthenticationValidationAndMissingSensors_ReturnExpectedErrors()
    {
        var sensor = await CreateSensor();
        foreach (var path in new[] { $"/api/sensors/{sensor.Id}/thermal-analyses", $"/api/sensors/{sensor.Id}/thermal-analysis/latest", "/api/thermal/map", "/api/thermal/overview" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync(path)).StatusCode);
        Authenticate(UserRoles.Citizen);
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?riskLevel=unknown", "?riskLevel=9", "?startDate=invalid", "?startDate=2026-11-12T00:00:00Z&endDate=2026-11-11T00:00:00Z" })
            Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/sensors/{sensor.Id}/thermal-analyses{query}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/sensors/{Guid.NewGuid()}/thermal-analyses")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/sensors/{Guid.NewGuid()}/thermal-analysis/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await Client.PostAsJsonAsync($"/api/sensors/{sensor.Id}/thermal-analyses", new { heatIndex = 30 })).StatusCode);
    }

    [Fact]
    public async Task FailedAnalysis_LeavesReadingPersisted_AndRecoversOnRetry()
    {
        var sensor = await CreateSensor();
        var reading = await AddReading(sensor.Id, DateTime.UtcNow);
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var repository = new FailingRepository(scope.ServiceProvider.GetRequiredService<IThermalAnalysisRepository>(), reading.Id);
        var useCase = Processor(repository);
        await useCase.Execute();
        Assert.True(await context.SensorReadings.AnyAsync(item => item.Id == reading.Id));
        Assert.False(await context.ThermalAnalyses.AnyAsync(item => item.SensorReadingId == reading.Id));
        await useCase.Execute();
        Assert.Equal(1, await context.ThermalAnalyses.CountAsync(item => item.SensorReadingId == reading.Id));
    }

    [Fact]
    public async Task PendingFailures_DoNotBlockLaterBatchesInPostgreSql()
    {
        var sensor = await CreateSensor();
        var date = DateTime.UtcNow;
        var valid = new SensorReading(sensor.Id, 33.1m, 56.4m, date);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            context.SensorReadings.AddRange(Enumerable.Range(0, 100).Select(index => new SensorReading(sensor.Id, -274, 60, date.AddMinutes(-101 + index))));
            context.SensorReadings.Add(valid);
            await context.SaveChangesAsync();
        }
        await Process();
        await using var verification = Factory.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        Assert.True(await db.ThermalAnalyses.AnyAsync(analysis => analysis.SensorReadingId == valid.Id));
        Assert.Equal(101, await db.SensorReadings.CountAsync(reading => reading.SensorId == sensor.Id));
    }

    [Fact]
    public async Task GeneratedReadings_AreAvailableToIndependentThermalProcessor()
    {
        var sensor = await CreateSensor();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var settings = new SensorSimulationSettings { MinTemperature = 33.1m, MaxTemperature = 33.1m, MinHumidity = 56.4m, MaxHumidity = 56.4m };
            await new GenerateSensorReadingsUseCase(new SensorRepository(context), new SensorReadingRepository(context), new UnitOfWork(context),
                new SensorReadingGenerator(settings, new Random(42), TimeProvider.System), settings, TimeProvider.System).Execute();
        }
        await Process();
        await using var verification = Factory.Services.CreateAsyncScope();
        var repository = verification.ServiceProvider.GetRequiredService<IThermalAnalysisRepository>();
        var analysis = await repository.GetLatest(sensor.Id, "1.0", default);
        Assert.NotNull(analysis);
        Assert.Equal(38.51m, analysis.Value.Analysis.HeatIndex);
    }

    private async Task<Sensor> CreateSensor(SensorStatus status = SensorStatus.ACTIVE, bool deleted = false)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var sensor = new Sensor(Guid.NewGuid().ToString("N"), "Thermal Test", null, -22.2935, -44.9332, null);
        sensor.UpdateStatus(status);
        if (deleted) sensor.Delete();
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        return sensor;
    }

    private async Task<SensorReading> AddReading(Guid sensorId, DateTime measuredAt)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var reading = new SensorReading(sensorId, 33.1m, 56.4m, measuredAt);
        context.SensorReadings.Add(reading);
        await context.SaveChangesAsync();
        return reading;
    }

    private async Task Process(string version = "1.0")
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await Processor(scope.ServiceProvider.GetRequiredService<IThermalAnalysisRepository>(), version).Execute();
    }

    private static ProcessPendingThermalAnalysesUseCase Processor(IThermalAnalysisRepository repository, string version = "1.0") =>
        new(repository, new HeatIndexCalculator(), new ThermalAnalysisSettings { AlgorithmVersion = version }, TimeProvider.System, NullLogger<ProcessPendingThermalAnalysesUseCase>.Instance);

    private void Authenticate(UserRoles role)
    {
        var user = new User("Thermal Test", Guid.NewGuid().ToString("N") + "@example.com", "hash");
        user.UpdateRole(role);
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        context.Users.Add(user);
        context.SaveChanges();
        var tokens = new JwtTokenService(new JwtSettings { SigningKey = UrbanShadeApplicationFactory.SigningKey });
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueSession(user));
    }

    private async Task<PagedResponse<ThermalAnalysisResponse>> History(Guid id, string query = "") =>
        (await Client.GetFromJsonAsync<PayloadResponse<PagedResponse<ThermalAnalysisResponse>>>($"/api/sensors/{id}/thermal-analyses{query}"))!.Data!;

    private sealed class FixedTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 11, 13, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FailingRepository : IThermalAnalysisRepository
    {
        private readonly IThermalAnalysisRepository _inner;
        private readonly Guid _readingId;
        private bool _failed;

        public FailingRepository(IThermalAnalysisRepository inner, Guid readingId) { _inner = inner; _readingId = readingId; }
        public Task<List<SensorReading>> GetPending(string algorithmVersion, int take, List<Guid> excludedReadingIds, CancellationToken cancellationToken) => _inner.GetPending(algorithmVersion, take, excludedReadingIds, cancellationToken);
        public Task<bool> TryAddForActiveSensor(ThermalAnalysis analysis, CancellationToken cancellationToken)
        {
            if (!_failed && analysis.SensorReadingId == _readingId) { _failed = true; throw new InvalidOperationException("Temporary analysis failure."); }
            return _inner.TryAddForActiveSensor(analysis, cancellationToken);
        }
        public Task<(SensorReading Reading, ThermalAnalysis Analysis)?> GetLatest(Guid sensorId, string algorithmVersion, CancellationToken cancellationToken) => _inner.GetLatest(sensorId, algorithmVersion, cancellationToken);
        public Task<List<(SensorReading Reading, ThermalAnalysis Analysis)>> GetLatestBySensors(List<Guid> sensorIds, string algorithmVersion, CancellationToken cancellationToken) => _inner.GetLatestBySensors(sensorIds, algorithmVersion, cancellationToken);
        public Task<(List<(SensorReading Reading, ThermalAnalysis Analysis)> Items, int TotalItems)> GetHistory(Guid sensorId, string algorithmVersion, DateTime? startDate, DateTime? endDate, ThermalRiskLevel? riskLevel, int page, int pageSize, CancellationToken cancellationToken) => _inner.GetHistory(sensorId, algorithmVersion, startDate, endDate, riskLevel, page, pageSize, cancellationToken);
    }
}
