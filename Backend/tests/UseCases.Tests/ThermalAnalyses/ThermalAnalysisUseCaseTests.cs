using Microsoft.Extensions.Logging.Abstractions;
using UrbanShade.Application.UseCases.ThermalAnalyses.GetMap;
using UrbanShade.Application.UseCases.ThermalAnalyses.GetOverview;
using UrbanShade.Application.UseCases.ThermalAnalyses.ProcessPending;
using UrbanShade.Communication.Responses.ThermalAnalyses;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.ThermalAnalysis;

namespace UseCases.Tests.ThermalAnalyses;

public sealed class ThermalAnalysisUseCaseTests
{
    [Theory]
    [InlineData(20, 50, 19.68)]
    [InlineData(26, 50, 25.98)]
    [InlineData(30, 70, 35.04)]
    [InlineData(40, 10, 36.71)]
    [InlineData(28, 90, 34.00)]
    [InlineData(33.1, 56.4, 38.51)]
    [InlineData(35, 60, 45.05)]
    [InlineData(30, 100, 44.36)]
    [InlineData(32.222222, 70, 41.07)]
    public void Calculator_MatchesNwsReferenceValuesInCelsius(decimal temperature, decimal humidity, decimal expected)
    {
        /// :: Reference: https://www.wpc.ncep.noaa.gov/html/heatindex_equation.shtml
        Assert.InRange(Math.Abs(new HeatIndexCalculator().Calculate(temperature, humidity) - expected), 0, 0.005m);
    }

    [Theory]
    [InlineData(-274, 50)]
    [InlineData(30, -1)]
    [InlineData(30, 101)]
    public void Calculator_RejectsInvalidReadings(decimal temperature, decimal humidity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeatIndexCalculator().Calculate(temperature, humidity));
    }

    [Theory]
    [InlineData(26.99, ThermalRiskLevel.LOW)]
    [InlineData(27, ThermalRiskLevel.MODERATE)]
    [InlineData(31.99, ThermalRiskLevel.MODERATE)]
    [InlineData(32, ThermalRiskLevel.HIGH)]
    [InlineData(38.99, ThermalRiskLevel.HIGH)]
    [InlineData(39, ThermalRiskLevel.CRITICAL)]
    public void Classification_UsesExactCelsiusBoundaries(decimal heatIndex, ThermalRiskLevel expected)
    {
        Assert.Equal(expected, new ThermalAnalysisSettings().Classify(heatIndex));
    }

    [Fact]
    public async Task Processing_ContinuesAfterFailure_AndCanRetryWithoutDuplicates()
    {
        var repository = new ProcessingRepository { FailFirst = true };
        repository.Readings.AddRange([new SensorReading(Guid.NewGuid(), 33.1m, 56.4m, DateTime.UtcNow), new SensorReading(Guid.NewGuid(), 35, 60, DateTime.UtcNow)]);
        var time = new FixedTime();
        var useCase = new ProcessPendingThermalAnalysesUseCase(repository, new HeatIndexCalculator(), new ThermalAnalysisSettings(), time,
            NullLogger<ProcessPendingThermalAnalysesUseCase>.Instance);

        await useCase.Execute();
        Assert.Equal(repository.Readings[1].Id, Assert.Single(repository.Analyses).SensorReadingId);
        await useCase.Execute();
        await useCase.Execute();
        Assert.Equal(2, repository.Analyses.Count);
        var analysis = Assert.Single(repository.Analyses, item => item.SensorReadingId == repository.Readings[0].Id);
        Assert.Equal(38.51m, analysis.HeatIndex);
        Assert.Equal(ThermalRiskLevel.HIGH, analysis.RiskLevel);
        Assert.Equal(time.GetUtcNow().UtcDateTime, analysis.AnalyzedAt);
    }

    [Fact]
    public async Task Processing_WhenDisabledDoesNotQuery_AndCancellationPropagates()
    {
        var repository = new ProcessingRepository();
        var settings = new ThermalAnalysisSettings { Enabled = false };
        var useCase = new ProcessPendingThermalAnalysesUseCase(repository, new HeatIndexCalculator(), settings, TimeProvider.System,
            NullLogger<ProcessPendingThermalAnalysesUseCase>.Instance);
        await useCase.Execute();
        Assert.Equal(0, repository.Queries);

        settings.Enabled = true;
        repository.Readings.Add(new SensorReading(Guid.NewGuid(), 30, 60, DateTime.UtcNow));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.Execute(cancellation.Token));
        Assert.Empty(repository.Analyses);
    }

    [Fact]
    public async Task Processing_DrainsMultipleBatches_EvenWhenFirstBatchFails()
    {
        var repository = new ProcessingRepository();
        repository.Readings.AddRange(Enumerable.Range(0, 100).Select(_ => new SensorReading(Guid.NewGuid(), -274, 60, DateTime.UtcNow)));
        repository.Readings.AddRange(Enumerable.Range(0, 105).Select(_ => new SensorReading(Guid.NewGuid(), 30, 60, DateTime.UtcNow)));
        await new ProcessPendingThermalAnalysesUseCase(repository, new HeatIndexCalculator(), new ThermalAnalysisSettings(), TimeProvider.System,
            NullLogger<ProcessPendingThermalAnalysesUseCase>.Instance).Execute();
        Assert.Equal(105, repository.Analyses.Count);
    }

    [Fact]
    public async Task Overview_UsesOnlyCurrentSensors_AndKeepsValuesInCelsius()
    {
        var map = new TestMap
        {
            Response = new ThermalMapResponse
            {
                Sensors =
                [
                    new() { Temperature = 30, HeatIndex = 35, RiskLevel = "HIGH", DataStatus = "CURRENT" },
                    new() { Temperature = 35, HeatIndex = 45, RiskLevel = "CRITICAL", DataStatus = "CURRENT" },
                    new() { Temperature = 50, HeatIndex = 80, RiskLevel = "CRITICAL", DataStatus = "STALE" },
                    new() { DataStatus = "NO_DATA" }
                ]
            }
        };
        var useCase = new GetThermalOverviewUseCase(map);
        var response = await useCase.Execute();
        Assert.Equal(4, response.TotalActiveSensors);
        Assert.Equal(2, response.SensorsWithCurrentData);
        Assert.Equal(32.5m, response.AverageTemperature);
        Assert.Equal(40, response.AverageHeatIndex);
        Assert.Equal(45, response.HighestHeatIndex);
        Assert.Equal(0, response.RiskDistribution.Low);
        Assert.Equal(0, response.RiskDistribution.Moderate);
        Assert.Equal(1, response.RiskDistribution.High);
        Assert.Equal(1, response.RiskDistribution.Critical);

        map.Response = new ThermalMapResponse { Sensors = [new() { DataStatus = "NO_DATA" }] };
        response = await useCase.Execute();
        Assert.Equal(1, response.TotalActiveSensors);
        Assert.Equal(0, response.SensorsWithCurrentData);
        Assert.Null(response.AverageTemperature);
        Assert.Null(response.AverageHeatIndex);
        Assert.Null(response.HighestHeatIndex);
        Assert.Equal(0, response.RiskDistribution.Low);
    }

    [Fact]
    public void Entity_RejectsInvalidIdentityVersionRiskAndTime()
    {
        Assert.Throws<ArgumentException>(() => new ThermalAnalysis(Guid.Empty, 30, ThermalRiskLevel.LOW, "1.0", DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => new ThermalAnalysis(Guid.NewGuid(), 30, ThermalRiskLevel.LOW, "", DateTime.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThermalAnalysis(Guid.NewGuid(), 30, (ThermalRiskLevel)9, "1.0", DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => new ThermalAnalysis(Guid.NewGuid(), 30, ThermalRiskLevel.LOW, "1.0", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)));
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 11, 13, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestMap : IGetThermalMapUseCase
    {
        public ThermalMapResponse Response { get; set; } = new();
        public Task<ThermalMapResponse> Execute(CancellationToken cancellationToken = default) => Task.FromResult(Response);
        public Task<ThermalMapResponse> Execute(Guid? cityId, Guid? organizationId, Guid? actorId, CancellationToken cancellationToken = default) => Task.FromResult(Response);
    }

    private sealed class ProcessingRepository : IThermalAnalysisRepository
    {
        public List<SensorReading> Readings { get; } = [];
        public List<ThermalAnalysis> Analyses { get; } = [];
        public bool FailFirst { get; set; }
        public int Queries { get; private set; }

        public Task<List<SensorReading>> GetPending(string algorithmVersion, int take, List<Guid> excludedReadingIds, CancellationToken cancellationToken)
        {
            Queries++;
            return Task.FromResult(Readings.Where(reading => !excludedReadingIds.Contains(reading.Id) && !Analyses.Any(analysis => analysis.SensorReadingId == reading.Id && analysis.AlgorithmVersion == algorithmVersion)).Take(take).ToList());
        }

        public Task<bool> TryAddForActiveSensor(ThermalAnalysis analysis, CancellationToken cancellationToken)
        {
            if (FailFirst) { FailFirst = false; throw new InvalidOperationException("Temporary persistence failure."); }
            Analyses.Add(analysis);
            return Task.FromResult(true);
        }

        public Task<(SensorReading Reading, ThermalAnalysis Analysis)?> GetLatest(Guid sensorId, string algorithmVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<List<(SensorReading Reading, ThermalAnalysis Analysis)>> GetLatestBySensors(List<Guid> sensorIds, string algorithmVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<(List<(SensorReading Reading, ThermalAnalysis Analysis)> Items, int TotalItems)> GetHistory(Guid sensorId, string algorithmVersion, DateTime? startDate, DateTime? endDate, ThermalRiskLevel? riskLevel, int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
