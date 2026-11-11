using System.Net;
using UrbanShade.Application.UseCases.Cities.Register;
using UrbanShade.Application.UseCases.Cities.GetAll;
using UrbanShade.Application.UseCases.Cities.GetById;
using UrbanShade.Communication.Requests.Cities;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UseCases.Tests.Cities;

public sealed class CityUseCaseTests
{
    [Fact]
    public async Task Register_NormalizesCity_AndMakesItAvailableForQueries()
    {
        var store = new TestStore();
        var response = await new RegisterCityUseCase(store, store).Execute(new RegisterCityRequest
        {
            Name = " São Paulo ", State = " sp ", IbgeCode = " 3550308 "
        });
        Assert.Equal("São Paulo", response.Name);
        Assert.Equal("SP", response.State);
        Assert.Equal("3550308", response.IbgeCode);
        Assert.True(store.Saved);
        Assert.Equal(response, await new GetCityByIdUseCase(store).Execute(response.Id));
        Assert.Equal(response, Assert.Single(await new GetCitiesUseCase(store).Execute()));
    }

    [Fact]
    public async Task Register_RejectsDuplicateIbgeCodeAndInvalidCity_WithoutSaving()
    {
        var store = new TestStore();
        store.Cities.Add(new City("São Paulo", "SP", "3550308"));
        var useCase = new RegisterCityUseCase(store, store);
        var duplicate = await Assert.ThrowsAsync<CityException>(() => useCase.Execute(new RegisterCityRequest
        {
            Name = "São Paulo", State = "SP", IbgeCode = " 3550308 "
        }));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.GetStatusCode());
        var invalid = await Assert.ThrowsAsync<CityException>(() => useCase.Execute(new RegisterCityRequest
        {
            Name = "São Paulo", State = "RJ", IbgeCode = "3550308"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.GetStatusCode());
        Assert.False(store.Saved);
        Assert.Single(store.Cities);
    }

    [Fact]
    public async Task GetById_RejectsUnknownCity()
    {
        var error = await Assert.ThrowsAsync<CityException>(() => new GetCityByIdUseCase(new TestStore()).Execute(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, error.GetStatusCode());
    }

    private sealed class TestStore : ICityRepository, IUnitOfWork
    {
        public List<City> Cities { get; } = [];
        public bool Saved { get; private set; }
        public Task<City?> FindById(Guid id, CancellationToken cancellationToken) => Task.FromResult(Cities.SingleOrDefault(city => city.Id == id));
        public Task<List<City>> GetAll(CancellationToken cancellationToken) => Task.FromResult(Cities);
        public Task<bool> IbgeCodeExists(string ibgeCode, CancellationToken cancellationToken) => Task.FromResult(Cities.Any(city => city.IbgeCode == ibgeCode));
        public void Add(City city) => Cities.Add(city);
        public Task SaveChanges(CancellationToken cancellationToken) { Saved = true; return Task.CompletedTask; }
        public Task<ITransaction> BeginTransaction(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
