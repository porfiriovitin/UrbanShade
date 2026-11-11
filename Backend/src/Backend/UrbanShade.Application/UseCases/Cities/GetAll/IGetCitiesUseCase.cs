using UrbanShade.Communication.Responses.Cities;

namespace UrbanShade.Application.UseCases.Cities.GetAll;

public interface IGetCitiesUseCase
{
    Task<List<CityResponse>> Execute(CancellationToken cancellationToken = default);
}
