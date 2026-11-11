using System.Net;
using System.Text.Json;
using UrbanShade.Application.UseCases.Sensors.Register;
using UrbanShade.Application.UseCases.Sensors.Update;
using UrbanShade.Application.UseCases.Sensors.UpdateStatus;
using UrbanShade.Application.UseCases.Sensors.Delete;
using UrbanShade.Application.UseCases.Sensors.GetById;
using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UseCases.Tests.Sensors;

public sealed class SensorUseCaseTests
{
    [Fact]
    public async Task Register_NormalizesAndSavesActiveSensor()
    {
        var store = new TestStore();
        var response = await new RegisterSensorUseCase(store, store, store).Execute(new RegisterSensorRequest
        {
            Code = " urb-001 ", Name = " Sensor ", Latitude = -22, Longitude = -44
        });
        Assert.Equal("URB-001", response.Code);
        Assert.Equal("Sensor", response.Name);
        Assert.Equal("ACTIVE", response.Status);
        Assert.True(store.Saved);
    }

    [Fact]
    public async Task Register_RejectsDuplicateWithoutSaving()
    {
        var store = new TestStore { Duplicate = true };
        var exception = await Assert.ThrowsAsync<SensorException>(() => new RegisterSensorUseCase(store, store, store).Execute(
            new RegisterSensorRequest { Code = "URB-001", Name = "Sensor", Latitude = 0, Longitude = 0 }));
        Assert.Equal(HttpStatusCode.Conflict, exception.GetStatusCode());
        Assert.False(store.Saved);
    }

    [Fact]
    public async Task Update_PreservesOmittedFieldsAndClearsOptionalFields()
    {
        var store = new TestStore { Sensor = new Sensor("URB-001", "Sensor", "Description", -22, -44, DateTime.UtcNow) };
        var request = JsonSerializer.Deserialize<UpdateSensorRequest>("""{"description":null,"installedAt":null,"latitude":-23}""", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var response = await new UpdateSensorUseCase(store, store, store).Execute(store.Sensor.Id, request);
        Assert.Equal("URB-001", response.Code);
        Assert.Equal("Sensor", response.Name);
        Assert.Equal(-23, response.Latitude);
        Assert.Equal(-44, response.Longitude);
        Assert.Null(response.Description);
        Assert.Null(response.InstalledAt);
        Assert.NotNull(response.UpdatedAt);
        Assert.True(store.Saved);
    }

    [Theory]
    [InlineData(SensorStatus.INACTIVE)]
    [InlineData(SensorStatus.MAINTENANCE)]
    public async Task Status_HidesSensorFromCommonUserButNotAdministrator(SensorStatus status)
    {
        var store = new TestStore { Sensor = new Sensor("URB-001", "Sensor", null, 0, 0, null) };
        await new UpdateSensorStatusUseCase(store, store).Execute(store.Sensor.Id, new UpdateSensorStatusRequest { Status = status.ToString() });
        var useCase = new GetSensorByIdUseCase(store);
        var exception = await Assert.ThrowsAsync<SensorException>(() => useCase.Execute(store.Sensor.Id, false));
        Assert.Equal(HttpStatusCode.NotFound, exception.GetStatusCode());
        Assert.Equal(status.ToString(), (await useCase.Execute(store.Sensor.Id, true)).Status);
        Assert.True(store.Sensor.Active);
    }

    [Fact]
    public async Task Delete_PreservesRecordAndRejectsFurtherOperations()
    {
        var store = new TestStore { Sensor = new Sensor("URB-001", "Sensor", null, 0, 0, null) };
        var id = store.Sensor.Id;
        await new DeleteSensorUseCase(store, store).Execute(id);
        Assert.False(store.Sensor.Active);
        Assert.NotNull(store.Sensor.DeletedAt);
        Assert.Equal(store.Sensor.DeletedAt, store.Sensor.UpdatedAt);
        var exception = await Assert.ThrowsAsync<SensorException>(() => new UpdateSensorUseCase(store, store, store).Execute(id, new UpdateSensorRequest { Name = "New" }));
        Assert.Equal(HttpStatusCode.NotFound, exception.GetStatusCode());
    }

    [Fact]
    public async Task CityAssignment_RegisterLinksCity_UpdatePreservesOrClearsIt()
    {
        var store = new TestStore { City = new City("São Paulo", "SP", "3550308") };
        var response = await new RegisterSensorUseCase(store, store, store).Execute(new RegisterSensorRequest
        {
            Code = "CITY-001", Name = "Sensor", Latitude = -23, Longitude = -46, CityId = store.City.Id
        });
        Assert.Equal(store.City.Id, response.CityId);

        var update = new UpdateSensorUseCase(store, store, store);
        Assert.Equal(store.City.Id, (await update.Execute(response.Id, new UpdateSensorRequest { Name = "Updated" })).CityId);
        var clear = JsonSerializer.Deserialize<UpdateSensorRequest>("""{"cityId":null}""", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Null((await update.Execute(response.Id, clear)).CityId);
    }

    [Fact]
    public async Task CityAssignment_RejectsUnknownCityBeforeChangingOrSavingSensor()
    {
        var store = new TestStore { Sensor = new Sensor("CITY-001", "Original", null, 0, 0, null) };
        var request = new RegisterSensorRequest { Code = "NEW", Name = "New", Latitude = 0, Longitude = 0, CityId = Guid.NewGuid() };
        await Assert.ThrowsAsync<CityException>(() => new RegisterSensorUseCase(store, store, store).Execute(request));
        await Assert.ThrowsAsync<CityException>(() => new UpdateSensorUseCase(store, store, store).Execute(store.Sensor.Id,
            new UpdateSensorRequest { Name = "Changed", CityId = Guid.NewGuid() }));
        Assert.Equal("Original", store.Sensor.Name);
        Assert.Null(store.Sensor.CityId);
        Assert.False(store.Saved);
    }

    private sealed class TestStore : ISensorRepository, IUnitOfWork, ICityRepository
    {
        public City? City { get; set; }
        public Sensor? Sensor { get; set; }
        public bool Duplicate { get; set; }
        public Task<Sensor?> FindForReadingHistory(Guid id, CancellationToken cancellationToken) => Task.FromResult(Sensor?.Id == id ? Sensor : null);
        public Task<List<Sensor>> GetActiveForReadingGeneration(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<List<Sensor>> GetActiveForThermalMap(Guid? cityId, Guid? organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public bool Saved { get; private set; }
        public Task<Sensor?> FindById(Guid id, CancellationToken cancellationToken) => Task.FromResult(Sensor?.Id == id && Sensor.DeletedAt is null ? Sensor : null);
        public Task<bool> CodeExists(string code, Guid? exceptId, CancellationToken cancellationToken) => Task.FromResult(Duplicate);
        public Task<List<Sensor>> GetAll(bool activeOnly, SensorStatus? status, string? code, string? name, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(Sensor sensor) => Sensor = sensor;
        public Task SaveChanges(CancellationToken cancellationToken) { Saved = true; return Task.CompletedTask; }
        public Task<ITransaction> BeginTransaction(CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<City?> ICityRepository.FindById(Guid id, CancellationToken cancellationToken) => Task.FromResult(City?.Id == id ? City : null);
        public Task<List<City>> GetAll(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> IbgeCodeExists(string ibgeCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(City city) => City = city;
    }
}
