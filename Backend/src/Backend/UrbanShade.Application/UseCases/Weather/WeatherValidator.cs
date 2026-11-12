using UrbanShade.Communication.Requests.Weather;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Weather;

public static class WeatherValidator
{
    public static void History(GetWeatherHistoryRequest request)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100 || (long)(request.Page - 1) * request.PageSize > int.MaxValue)
            throw new WeatherException(ErrorKind.Validation, "Provide a valid page and a page size between 1 and 100.");
        if ((request.StartDate.HasValue && request.StartDate.Value.Kind != DateTimeKind.Utc) || (request.EndDate.HasValue && request.EndDate.Value.Kind != DateTimeKind.Utc) || (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate))
            throw new WeatherException(ErrorKind.Validation, "Provide a valid UTC weather history period.");
    }
}
