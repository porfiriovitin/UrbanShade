using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UrbanShade.Infraestructure.Workers;
using UrbanShade.Application.UseCases.SensorReadings.Generate;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Simulation;
using UrbanShade.Domain.ThermalAnalysis;
using UrbanShade.Application.UseCases.ThermalAnalyses.ProcessPending;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.DataAcess.Repositories;

namespace WebApi.Tests.SensorReadings;

public sealed class SensorSimulationTests : IClassFixture<UrbanShadeApplicationFactory>
{
    private readonly UrbanShadeApplicationFactory _factory;

    public SensorSimulationTests(UrbanShadeApplicationFactory factory)
    {
        _factory = factory;
        _factory.CreateClient();
    }

    [Fact]
    public async Task ConcurrentGenerators_ProduceOneReadingPerInterval()
    {
        var sensor = Sensor();
        await using (var context = Context()) { context.Sensors.Add(sensor); await context.SaveChangesAsync(); }
        await using var first = Context();
        await using var second = Context();
        var settings = new SensorSimulationSettings();
        await Task.WhenAll(Cycle(first, settings).Execute(), Cycle(second, settings).Execute());
        await using var verification = Context();
        Assert.Equal(1, await verification.SensorReadings.CountAsync(item => item.SensorId == sensor.Id));
        await verification.SensorReadings.Where(item => item.SensorId == sensor.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.MeasuredAt, DateTime.UtcNow.AddMinutes(-settings.IntervalMinutes - 1)));
        await Cycle(verification, settings).Execute();
        Assert.Equal(2, await verification.SensorReadings.CountAsync(item => item.SensorId == sensor.Id));
    }

    [Fact]
    public async Task Generation_PersistsOnlyForActiveSensors_AndLoadsPreviousValuesInTwoQueries()
    {
        var settings = new SensorSimulationSettings();
        Guid activeId;
        List<Guid> hiddenIds = [];
        await using (var context = Context())
        {
            var active = Sensor();
            activeId = active.Id;
            context.Sensors.Add(active);
            foreach (var status in new[] { SensorStatus.INACTIVE, SensorStatus.MAINTENANCE })
            {
                var hidden = Sensor();
                hidden.UpdateStatus(status);
                hiddenIds.Add(hidden.Id);
                context.Sensors.Add(hidden);
            }
            var deleted = Sensor();
            deleted.Delete();
            hiddenIds.Add(deleted.Id);
            context.Sensors.Add(deleted);
            context.SensorReadings.Add(new SensorReading(activeId, 30, 60, DateTime.UtcNow.AddMinutes(-5)));
            await context.SaveChangesAsync();
        }

        var counter = new SelectCounter();
        await using (var context = Context(counter))
            await Cycle(context, settings).Execute();

        Assert.Equal(2, counter.Selects);
        await using (var context = Context())
        {
            var readings = await context.SensorReadings.Where(reading => reading.SensorId == activeId).OrderBy(reading => reading.MeasuredAt).ToListAsync();
            Assert.Equal(2, readings.Count);
            Assert.InRange(Math.Abs(readings[1].Temperature - readings[0].Temperature), 0, settings.MaxTemperatureVariation);
            Assert.InRange(Math.Abs(readings[1].Humidity - readings[0].Humidity), 0, settings.MaxHumidityVariation);
            Assert.Equal(DateTimeKind.Utc, readings[1].MeasuredAt.Kind);
            Assert.False(await context.SensorReadings.AnyAsync(reading => hiddenIds.Contains(reading.SensorId)));
        }

        /// :: A new scope resumes from the last persisted reading.
        await using (var context = Context())
            await Cycle(context, settings).Execute();
        await using (var context = Context())
            Assert.Equal(2, await context.SensorReadings.CountAsync(reading => reading.SensorId == activeId));
    }

    [Fact]
    public async Task Generation_RollsBackEntireCycleWhenPersistenceFails_ThenCanRetry()
    {
        var active = Sensor();
        await using (var context = Context())
        {
            context.Sensors.Add(active);
            await context.SaveChangesAsync();
        }

        await using (var context = Context())
        {
            var readings = new InvalidReadingRepository(new SensorReadingRepository(context));
            var settings = new SensorSimulationSettings();
            var useCase = new GenerateSensorReadingsUseCase(new SensorRepository(context), readings, new UnitOfWork(context),
                new SensorReadingGenerator(settings, new Random(42), TimeProvider.System), settings, TimeProvider.System);
            await Assert.ThrowsAsync<DbUpdateException>(() => useCase.Execute());
        }

        await using (var context = Context())
        {
            Assert.False(await context.SensorReadings.AnyAsync(reading => reading.SensorId == active.Id));
            await Cycle(context, new SensorSimulationSettings()).Execute();
            Assert.True(await context.SensorReadings.AnyAsync(reading => reading.SensorId == active.Id));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generation_SerializesWithConcurrentDeactivationOrDeletion(bool delete)
    {
        var active = Sensor();
        await using (var context = Context())
        {
            context.Sensors.Add(active);
            await context.SaveChangesAsync();
        }

        await using var generationContext = Context();
        await using var updateContext = Context();
        var gate = new GatedSensorRepository(new SensorRepository(generationContext));
        var settings = new SensorSimulationSettings();
        var useCase = new GenerateSensorReadingsUseCase(gate, new SensorReadingRepository(generationContext), new UnitOfWork(generationContext),
            new SensorReadingGenerator(settings, new Random(42), TimeProvider.System), settings, TimeProvider.System);
        var generation = useCase.Execute();
        await gate.Locked.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var sensor = await updateContext.Sensors.SingleAsync(sensor => sensor.Id == active.Id);
        if (delete) sensor.Delete();
        else sensor.UpdateStatus(SensorStatus.INACTIVE);

        var update = updateContext.SaveChangesAsync();
        try
        {
            /// :: The update cannot commit while the cycle holds the sensor lock.
            await Assert.ThrowsAsync<TimeoutException>(() => update.WaitAsync(TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            gate.Release.TrySetResult();
        }
        await generation.WaitAsync(TimeSpan.FromSeconds(5));
        await update.WaitAsync(TimeSpan.FromSeconds(5));

        await using var verification = Context();
        Assert.Equal(1, await verification.SensorReadings.CountAsync(reading => reading.SensorId == active.Id));
        await Cycle(verification, settings).Execute();
        Assert.Equal(1, await verification.SensorReadings.CountAsync(reading => reading.SensorId == active.Id));
    }

    [Fact]
    public async Task Worker_WhenDisabledDoesNotResolveGeneration()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var logger = new RecordingLogger();
        using var worker = new SensorSimulationBackgroundService(services.GetRequiredService<IServiceScopeFactory>(),
            new SensorSimulationSettings { Enabled = false }, new ThermalAnalysisSettings { Enabled = false }, TimeProvider.System, logger);
        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(logger.Errors);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Worker_RecoversAfterFailure_UsesFreshScopesAndConfiguredInterval()
    {
        var state = new WorkerState { FailFirst = true };
        var time = new RecordingTime();
        var logger = new RecordingLogger();
        using var services = new ServiceCollection().AddScoped<IGenerateSensorReadingsUseCase>(_ => new ProbeUseCase(state)).BuildServiceProvider();
        using var worker = new SensorSimulationBackgroundService(services.GetRequiredService<IServiceScopeFactory>(),
            new SensorSimulationSettings { IntervalMinutes = 0.001 }, new ThermalAnalysisSettings { Enabled = false }, time, logger);

        await worker.StartAsync(CancellationToken.None);
        await state.SecondCycle.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);
        Assert.True(state.Calls >= 2);
        Assert.Single(logger.Errors);
        Assert.True(state.Disposals >= 2);
        Assert.All(time.Delays, delay => Assert.Equal(TimeSpan.FromMinutes(0.001), delay));
        Assert.NotEmpty(time.Delays);
    }

    [Fact]
    public async Task Worker_DoesNotOverlapCycles_AndCancelsInFlightWork()
    {
        var state = new WorkerState { Block = true };
        using var services = new ServiceCollection().AddScoped<IGenerateSensorReadingsUseCase>(_ => new ProbeUseCase(state)).BuildServiceProvider();
        using var worker = new SensorSimulationBackgroundService(services.GetRequiredService<IServiceScopeFactory>(),
            new SensorSimulationSettings { IntervalMinutes = 0.001 }, new ThermalAnalysisSettings { Enabled = false }, TimeProvider.System, new RecordingLogger());

        await worker.StartAsync(CancellationToken.None);
        await state.FirstCycle.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(30);
        Assert.Equal(1, state.Calls);
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(state.Cancelled);
        Assert.Equal(1, state.Disposals);
    }

    [Fact]
    public async Task Worker_ProcessesThermalDataWithSimulationDisabled_AndRetriesWithFreshScopes()
    {
        var state = new WorkerState { FailFirst = true };
        var logger = new RecordingLogger();
        using var services = new ServiceCollection().AddScoped<IProcessPendingThermalAnalysesUseCase>(_ => new ThermalProbeUseCase(state)).BuildServiceProvider();
        using var worker = new SensorSimulationBackgroundService(services.GetRequiredService<IServiceScopeFactory>(),
            new SensorSimulationSettings { Enabled = false, IntervalMinutes = 0.001 }, new ThermalAnalysisSettings(), TimeProvider.System, logger);

        await worker.StartAsync(CancellationToken.None);
        await state.SecondCycle.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);
        Assert.True(state.Calls >= 2);
        Assert.True(state.Disposals >= 2);
        Assert.Single(logger.Errors);
    }

    [Fact]
    public async Task Worker_ProcessesPendingAnalysesEvenWhenGenerationFails()
    {
        var generation = new WorkerState { FailFirst = true };
        var thermal = new WorkerState();
        var logger = new RecordingLogger();
        using var services = new ServiceCollection()
            .AddScoped<IGenerateSensorReadingsUseCase>(_ => new ProbeUseCase(generation))
            .AddScoped<IProcessPendingThermalAnalysesUseCase>(_ => new ThermalProbeUseCase(thermal))
            .BuildServiceProvider();
        using var worker = new SensorSimulationBackgroundService(services.GetRequiredService<IServiceScopeFactory>(),
            new SensorSimulationSettings { IntervalMinutes = 1 }, new ThermalAnalysisSettings(), TimeProvider.System, logger);

        await worker.StartAsync(CancellationToken.None);
        await thermal.FirstCycle.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);
        Assert.Equal(1, generation.Calls);
        Assert.Equal(1, thermal.Calls);
        Assert.Equal(1, generation.Disposals);
        Assert.Equal(1, thermal.Disposals);
        Assert.Single(logger.Errors);
    }

    private UrbanShadeDbContext Context(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<UrbanShadeDbContext>()
        .UseNpgsql(_factory.ConnectionString, postgres => postgres.UseNetTopologySuite()).AddInterceptors(interceptors).Options);

    private static Sensor Sensor() => new(Guid.NewGuid().ToString("N"), "Simulation Test", null, -22, -44, null);

    private static GenerateSensorReadingsUseCase Cycle(UrbanShadeDbContext context, SensorSimulationSettings settings) =>
        new(new SensorRepository(context), new SensorReadingRepository(context), new UnitOfWork(context),
            new SensorReadingGenerator(settings, new Random(42), TimeProvider.System), settings, TimeProvider.System);

    private sealed class SelectCounter : DbCommandInterceptor
    {
        public int Selects { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) Selects++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class InvalidReadingRepository : ISensorReadingRepository
    {
        private readonly ISensorReadingRepository _inner;
        public InvalidReadingRepository(ISensorReadingRepository inner) { _inner = inner; }
        public void AddRange(List<SensorReading> readings)
        {
            _inner.AddRange(readings);
            _inner.AddRange([new SensorReading(Guid.NewGuid(), 30, 60, DateTime.UtcNow)]);
        }
        public Task<List<SensorReading>> GetLatestBySensors(List<Guid> sensorIds, CancellationToken cancellationToken) => _inner.GetLatestBySensors(sensorIds, cancellationToken);
        public Task<SensorReading?> GetLatest(Guid sensorId, CancellationToken cancellationToken) => _inner.GetLatest(sensorId, cancellationToken);
        public Task<(List<SensorReading> Items, int TotalItems)> GetHistory(Guid sensorId, DateTime? startDate, DateTime? endDate, int page, int pageSize, CancellationToken cancellationToken) => _inner.GetHistory(sensorId, startDate, endDate, page, pageSize, cancellationToken);
    }

    private sealed class GatedSensorRepository : ISensorRepository
    {
        private readonly ISensorRepository _inner;
        public GatedSensorRepository(ISensorRepository inner) { _inner = inner; }
        public Task<List<Sensor>> GetActiveForThermalMap(Guid? cityId, Guid? organizationId, CancellationToken cancellationToken) => _inner.GetActiveForThermalMap(cityId, organizationId, cancellationToken);
        public TaskCompletionSource Locked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<List<Sensor>> GetActiveForReadingGeneration(CancellationToken cancellationToken)
        {
            var sensors = await _inner.GetActiveForReadingGeneration(cancellationToken);
            Locked.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            return sensors;
        }
        public Task<Sensor?> FindById(Guid id, CancellationToken cancellationToken) => _inner.FindById(id, cancellationToken);
        public Task<Sensor?> FindForReadingHistory(Guid id, CancellationToken cancellationToken) => _inner.FindForReadingHistory(id, cancellationToken);
        public Task<bool> CodeExists(string code, Guid? exceptId, CancellationToken cancellationToken) => _inner.CodeExists(code, exceptId, cancellationToken);
        public Task<List<Sensor>> GetAll(bool activeOnly, SensorStatus? status, string? code, string? name, CancellationToken cancellationToken, int page = 1, int pageSize = 100) => _inner.GetAll(activeOnly, status, code, name, cancellationToken, page, pageSize);
        public void Add(Sensor sensor) => _inner.Add(sensor);
    }

    private sealed class WorkerState
    {
        public int Calls;
        public int Disposals;
        public bool FailFirst;
        public bool Block;
        public bool Cancelled;
        public TaskCompletionSource FirstCycle { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondCycle { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ThermalProbeUseCase : IProcessPendingThermalAnalysesUseCase, IDisposable
    {
        private readonly ProbeUseCase _probe;
        public ThermalProbeUseCase(WorkerState state) { _probe = new ProbeUseCase(state); }
        public Task Execute(CancellationToken cancellationToken = default) => _probe.Execute(cancellationToken);
        public void Dispose() => _probe.Dispose();
    }

    private sealed class ProbeUseCase : IGenerateSensorReadingsUseCase, IDisposable
    {
        private readonly WorkerState _state;
        public ProbeUseCase(WorkerState state) { _state = state; }
        public async Task Execute(CancellationToken cancellationToken = default)
        {
            var count = Interlocked.Increment(ref _state.Calls);
            _state.FirstCycle.TrySetResult();
            if (_state.FailFirst && count == 1) throw new InvalidOperationException("Simulated cycle failure.");
            if (count >= 2) _state.SecondCycle.TrySetResult();
            if (_state.Block)
            {
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { _state.Cancelled = true; throw; }
            }
        }
        public void Dispose() => Interlocked.Increment(ref _state.Disposals);
    }

    private sealed class RecordingTime : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            return TimeProvider.System.CreateTimer(callback, state, dueTime, period);
        }
    }

    private sealed class RecordingLogger : ILogger<SensorSimulationBackgroundService>
    {
        public List<Exception> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) Errors.Add(exception);
        }
    }
}
