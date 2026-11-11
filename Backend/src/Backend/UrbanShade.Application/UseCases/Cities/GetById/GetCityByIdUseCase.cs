using System.Net;
using UrbanShade.Communication.Responses.Cities;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Cities.GetById;

public sealed class GetCityByIdUseCase : IGetCityByIdUseCase
{
    private readonly ICityRepository _cities;

    public GetCityByIdUseCase(ICityRepository cities)
    {
        _cities = cities;
    }

    public async Task<CityResponse> Execute(Guid id, CancellationToken cancellationToken = default)
    {
        var city = await _cities.FindById(id, cancellationToken);
        if (city is null) throw new CityException(HttpStatusCode.NotFound, "City is unavailable.");
        return ToResponse(city);
    }

    internal static CityResponse ToResponse(City city) => new()
    {
        Id = city.Id,
        Name = city.Name,
        State = city.State,
        IbgeCode = city.IbgeCode
    };
}
