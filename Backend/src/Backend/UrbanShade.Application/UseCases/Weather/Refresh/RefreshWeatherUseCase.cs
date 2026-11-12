using UrbanShade.Application.Services;
using UrbanShade.Application.UseCases.Weather.Query;
using UrbanShade.Communication.Responses.Weather;

namespace UrbanShade.Application.UseCases.Weather.Refresh;

public sealed class RefreshWeatherUseCase(WeatherService service, IQueryWeatherUseCase query) : IRefreshWeatherUseCase
{
    public async Task<WeatherCurrentResponse> Execute(Guid regionId, CancellationToken cancellationToken)
    {
        await service.Refresh(regionId, cancellationToken);

        return await query.Current(regionId, cancellationToken);
    }
}
