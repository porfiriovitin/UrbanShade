using UrbanShade.Application.UseCases.Cities.GetById;
using UrbanShade.Communication.Responses.Cities;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.Cities.GetAll;

public sealed class GetCitiesUseCase : IGetCitiesUseCase
{
    private readonly ICityRepository _cities;

    public GetCitiesUseCase(ICityRepository cities)
    {
        _cities = cities;
    }

    public async Task<List<CityResponse>> Execute(CancellationToken cancellationToken = default)
    {
        var cities = await _cities.GetAll(cancellationToken);

        return (cities).Select(GetCityByIdUseCase.ToResponse).ToList();
    }
}
