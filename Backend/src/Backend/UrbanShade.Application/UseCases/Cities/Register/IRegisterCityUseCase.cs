using UrbanShade.Communication.Requests.Cities;
using UrbanShade.Communication.Responses.Cities;

namespace UrbanShade.Application.UseCases.Cities.Register;

public interface IRegisterCityUseCase
{
    Task<CityResponse> Execute(RegisterCityRequest request, CancellationToken cancellationToken = default);
}
