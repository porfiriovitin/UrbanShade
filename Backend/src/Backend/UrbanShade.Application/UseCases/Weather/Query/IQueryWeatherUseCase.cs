using UrbanShade.Communication.Requests.Weather;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Weather;

namespace UrbanShade.Application.UseCases.Weather.Query;

public interface IQueryWeatherUseCase
{
    Task<WeatherCurrentResponse> Current(Guid regionId, CancellationToken cancellationToken);
    Task<WeatherForecastResponse> Forecast(Guid regionId, int? days, CancellationToken cancellationToken);
    Task<PagedResponse<WeatherCurrentResponse>> History(Guid regionId, GetWeatherHistoryRequest request, bool administrator, CancellationToken cancellationToken);
    Task<WeatherOverviewResponse> Overview(CancellationToken cancellationToken, Guid? cityId = null);
}
