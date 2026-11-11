using UrbanShade.Communication.Responses.Weather;

namespace UrbanShade.Application.UseCases.Weather.Refresh;

public interface IRefreshWeatherUseCase
{
    Task<WeatherCurrentResponse> Execute(Guid regionId, CancellationToken cancellationToken);
}
