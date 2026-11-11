using UrbanShade.Communication.Responses.Cities;

namespace UrbanShade.Application.UseCases.Cities.GetById;

public interface IGetCityByIdUseCase
{
    Task<CityResponse> Execute(Guid id, CancellationToken cancellationToken = default);
}
