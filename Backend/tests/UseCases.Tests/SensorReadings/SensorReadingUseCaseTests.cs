using System.Net;
using UrbanShade.Application.UseCases.SensorReadings.Generate;
using UrbanShade.Application.UseCases.SensorReadings.GetHistory;
using UrbanShade.Application.UseCases.SensorReadings.GetLatest;
using UrbanShade.Application.UseCases.SensorReadings.GetLatestBySensors;
using UrbanShade.Communication.Requests.SensorReadings;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Simulation;
using UrbanShade.Exceptions;

namespace UseCases.Tests.SensorReadings;

public sealed class SensorReadingUseCaseTests
{
    [Fact]
    public void Generator_ProducesGradualReadingsWithinLimitsAndUtc()
    {
        var settings = new SensorSimulationSettings();
        var time = new FixedTime();
        var generator = new SensorReadingGenerator(settings, new Random(42), time);
        var id = Guid.NewGuid();
        SensorReading? previous = null;

        for (var i = 0; i < 1000; i++)
        {
            var reading = generator.Generate(id, previous);
            Assert.InRange(reading.Temperature, settings.MinTemperature, settings.MaxTemperature);
            Assert.InRange(reading.Humidity, settings.MinHumidity, settings.MaxHumidity);
            Assert.Equal(time.GetUtcNow().UtcDateTime, reading.MeasuredAt);
            Assert.Equal(DateTimeKind.Utc, reading.MeasuredAt.Kind);
            if (previous is not null)
            {
                Assert.InRange(Math.Abs(reading.Temperature - previous.Temperature), 0, settings.MaxTemperatureVariation);
                Assert.InRange(Math.Abs(reading.Humidity - previous.Humidity), 0, settings.MaxHumidityVariation);
            }
            previous = reading;
        }
    }

    [Fact]
    public void Generator_HandlesZeroVariationAndChangedLimits()
    {
        var id = Guid.NewGuid();
        var settings = new SensorSimulationSettings { MaxTemperatureVariation = 0, MaxHumidityVariation = 0 };
        var generator = new SensorReadingGenerator(settings, new Random(42), new FixedTime());
        var previous = new SensorReading(id, 30, 60, DateTime.UtcNow);
        var same = generator.Generate(id, previous);
        Assert.Equal(30, same.Temperature);
        Assert.Equal(60, same.Humidity);
        var rebased = generator.Generate(id, new SensorReading(id, 50, 100, DateTime.UtcNow));
        Assert.Equal(settings.MaxTemperature, rebased.Temperature);
        Assert.Equal(settings.MaxHumidity, rebased.Humidity);
    }

    [Fact]
    public async Task Generate_LoadsPreviousReadingsInBatchAndCommits()
    {
        var store = new TestStore();
        store.Sensors.Add(new Sensor("ONE", "One", null, 0, 0, null));
        store.Sensors.Add(new Sensor("TWO", "Two", null, 0, 0, null));
        var settings = new SensorSimulationSettings();
        await new GenerateSensorReadingsUseCase(store, store, store, new SensorReadingGenerator(settings, new Random(42), new FixedTime()), settings, new FixedTime()).Execute();
        Assert.Equal(2, store.Readings.Count);
        Assert.Equal(1, store.BatchCalls);
        Assert.True(store.Saved);
        Assert.True(store.Committed);
        Assert.True(store.Disposed);
    }

    [Fact]
    public async Task Generate_WhenDisabledDoesNotOpenTransaction()
    {
        var store = new TestStore();
        var settings = new SensorSimulationSettings { Enabled = false };
        await new GenerateSensorReadingsUseCase(store, store, store, new SensorReadingGenerator(settings, new Random(42), new FixedTime()), settings, new FixedTime()).Execute();
        Assert.False(store.Began);
        Assert.Empty(store.Readings);
    }

    [Fact]
    public async Task Generate_WhenPersistenceFailsDoesNotCommitAndDisposesTransaction()
    {
        var store = new TestStore { FailSave = true };
        store.Sensors.Add(new Sensor("ONE", "One", null, 0, 0, null));
        var settings = new SensorSimulationSettings();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new GenerateSensorReadingsUseCase(store, store, store,
            new SensorReadingGenerator(settings, new Random(42), new FixedTime()), settings, new FixedTime()).Execute());
        Assert.False(store.Committed);
        Assert.True(store.Disposed);
    }

    [Theory]
    [InlineData(SensorStatus.INACTIVE)]
    [InlineData(SensorStatus.MAINTENANCE)]
    public async Task History_InactiveSensorIsVisibleOnlyToAdministrator(SensorStatus status)
    {
        var store = new TestStore();
        var sensor = new Sensor("ONE", "One", null, 0, 0, null);
        sensor.UpdateStatus(status);
        store.Sensors.Add(sensor);
        var useCase = new GetSensorReadingsUseCase(store, store);
        var error = await Assert.ThrowsAsync<SensorException>(() => useCase.Execute(sensor.Id, new GetSensorReadingsRequest(), false));
        Assert.Equal(UrbanShade.Exceptions.ErrorKind.NotFound, error.GetErrorKind());
        Assert.Empty((await useCase.Execute(sensor.Id, new GetSensorReadingsRequest(), true)).Items);
    }

    [Fact]
    public async Task History_DeletedSensorIsVisibleOnlyToAdministrator_AndLatestRemainsHidden()
    {
        var store = new TestStore();
        var sensor = new Sensor("ONE", "One", null, 0, 0, null);
        sensor.Delete();
        store.Sensors.Add(sensor);
        var useCase = new GetSensorReadingsUseCase(store, store);
        await Assert.ThrowsAsync<SensorException>(() => useCase.Execute(sensor.Id, new GetSensorReadingsRequest(), false));
        Assert.Empty((await useCase.Execute(sensor.Id, new GetSensorReadingsRequest(), true)).Items);
        await Assert.ThrowsAsync<SensorException>(() => new GetLatestSensorReadingUseCase(store, store).Execute(sensor.Id, true));
    }

    [Fact]
    public async Task History_NormalizesDatesToUtc_AndReturnsPagingMetadata()
    {
        var store = new TestStore();
        var sensor = new Sensor("ONE", "One", null, 0, 0, null);
        store.Sensors.Add(sensor);
        var start = DateTimeOffset.Parse("2026-11-11T10:00:00-03:00");
        var response = await new GetSensorReadingsUseCase(store, store).Execute(sensor.Id,
            new GetSensorReadingsRequest { StartDate = start, EndDate = start.AddHours(1), Page = 2, PageSize = 5 }, false);
        Assert.Equal(start.UtcDateTime, store.Start);
        Assert.Equal(start.AddHours(1).UtcDateTime, store.End);
        Assert.Equal(2, response.Page);
        Assert.Equal(5, response.PageSize);
    }

    [Fact]
    public async Task Latest_EmptySensorReturnsNull_AndMapReturnsOnlyReadingsWithCoordinates()
    {
        var store = new TestStore();
        var sensor = new Sensor("ONE", "One", null, -22, -44, null);
        store.Sensors.Add(sensor);
        Assert.Null(await new GetLatestSensorReadingUseCase(store, store).Execute(sensor.Id, false));
        store.Readings.Add(new SensorReading(sensor.Id, 30, 60, DateTime.UtcNow));
        var response = Assert.Single(await new GetLatestSensorReadingsUseCase(store, store).Execute());
        Assert.Equal(-22, response.Latitude);
        Assert.Equal(-44, response.Longitude);
        Assert.Equal("One", response.SensorName);
        Assert.True(response.IsSimulated);
    }

    [Fact]
    public void Entity_RejectsInvalidHumiditySensorAndTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SensorReading(Guid.NewGuid(), 30, 101, DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => new SensorReading(Guid.Empty, 30, 50, DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => new SensorReading(Guid.NewGuid(), 30, 50, DateTime.Now));
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 11, 13, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestStore : ISensorRepository, ISensorReadingRepository, IUnitOfWork, ITransaction
    {
        public List<Sensor> Sensors { get; } = [];
        public List<SensorReading> Readings { get; } = [];
        public bool Saved { get; private set; }
        public bool Committed { get; private set; }
        public bool Disposed { get; private set; }
        public bool Began { get; private set; }
        public bool FailSave { get; set; }
        public int BatchCalls { get; private set; }
        public DateTime? Start { get; private set; }
        public DateTime? End { get; private set; }

        public Task<Sensor?> FindById(Guid id, CancellationToken cancellationToken) => Task.FromResult(Sensors.SingleOrDefault(sensor => sensor.Id == id && sensor.DeletedAt is null));
        public Task<Sensor?> FindForReadingHistory(Guid id, CancellationToken cancellationToken) => Task.FromResult(Sensors.SingleOrDefault(sensor => sensor.Id == id));
        public Task<List<Sensor>> GetActiveForReadingGeneration(CancellationToken cancellationToken) => Task.FromResult(Sensors.Where(sensor => sensor.Status == SensorStatus.ACTIVE && sensor.DeletedAt is null && sensor.Active).ToList());
        public Task<List<Sensor>> GetActiveForThermalMap(Guid? cityId, Guid? organizationId, CancellationToken cancellationToken) => GetActiveForReadingGeneration(cancellationToken);
        public Task<List<Sensor>> GetAll(bool activeOnly, SensorStatus? status, string? code, string? name, CancellationToken cancellationToken, int page = 1, int pageSize = 100) => GetActiveForReadingGeneration(cancellationToken);
        public Task<bool> CodeExists(string code, Guid? exceptId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(Sensor sensor) => Sensors.Add(sensor);
        public void AddRange(List<SensorReading> readings) => Readings.AddRange(readings);
        public Task<SensorReading?> GetLatest(Guid sensorId, CancellationToken cancellationToken) => Task.FromResult(Readings.FirstOrDefault(reading => reading.SensorId == sensorId));
        public Task<List<SensorReading>> GetLatestBySensors(List<Guid> sensorIds, CancellationToken cancellationToken)
        {
            BatchCalls++;
            return Task.FromResult(Readings.Where(reading => sensorIds.Contains(reading.SensorId)).ToList());
        }
        public Task<(List<SensorReading> Items, int TotalItems)> GetHistory(Guid sensorId, DateTime? startDate, DateTime? endDate, int page, int pageSize, CancellationToken cancellationToken)
        {
            Start = startDate;
            End = endDate;
            return Task.FromResult((Readings.Where(reading => reading.SensorId == sensorId).ToList(), Readings.Count));
        }
        public Task SaveChanges(CancellationToken cancellationToken)
        {
            if (FailSave) throw new InvalidOperationException("Persistence failed.");
            Saved = true;
            return Task.CompletedTask;
        }
        public Task<ITransaction> BeginTransaction(CancellationToken cancellationToken) { Began = true; return Task.FromResult<ITransaction>(this); }
        public Task Commit(CancellationToken cancellationToken) { Committed = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
