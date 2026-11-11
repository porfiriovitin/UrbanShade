using System.Net;
using Microsoft.Extensions.Options;
using UrbanShade.Communication.Requests.Weather;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.Weather;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Weather;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Weather.Query;

public sealed class QueryWeatherUseCase : IQueryWeatherUseCase
{
    private readonly IGeoRegionRepository _regions;
    private readonly IWeatherRepository _weather;
    private readonly OpenMeteoSettings _settings;
    private readonly TimeProvider _time;
    public QueryWeatherUseCase(IGeoRegionRepository regions, IWeatherRepository weather, IOptions<OpenMeteoSettings> settings, TimeProvider time)
    { _regions = regions; _weather = weather; _settings = settings.Value; _time = time; }

    private async Task<GeoRegion> Region(Guid id, bool administrator, CancellationToken cancellationToken)
    {
        var region = await _regions.FindIncludingInactive(id, cancellationToken);
        if (region is null) throw new WeatherException(HttpStatusCode.NotFound, "Region not found.");
        if (!region.Active && !administrator) throw new WeatherException(HttpStatusCode.NotFound, "Active region not found.");
        return region;
    }
    private string Status(DateTime? fetchedAt, DateTime? observedAt = null)
    {
        if (!fetchedAt.HasValue) return "NO_DATA";
        var cutoff = _time.GetUtcNow().UtcDateTime.AddMinutes(-_settings.StaleDataThresholdMinutes);
        return fetchedAt <= cutoff || observedAt <= cutoff ? "STALE" : "CURRENT";
    }
    private WeatherCurrentResponse Response(GeoRegion region, WeatherSnapshot? snapshot) => new(region.Id, region.Name,
        Status(snapshot?.FetchedAt, snapshot?.ObservedAt), snapshot?.Temperature, snapshot?.Humidity, snapshot?.ApparentTemperature,
        snapshot?.WindSpeed, snapshot?.Precipitation, snapshot?.CloudCover, snapshot?.WeatherCode, snapshot?.ObservedAt,
        snapshot?.FetchedAt, snapshot?.Latitude, snapshot?.Longitude, snapshot?.BoundaryVersion) { Model = snapshot?.Model };

    public async Task<WeatherCurrentResponse> Current(Guid regionId, CancellationToken cancellationToken)
    {
        var region = await Region(regionId, false, cancellationToken);
        return Response(region, await _weather.GetLatest(regionId, region.BoundaryVersion, cancellationToken));
    }
    public async Task<WeatherForecastResponse> Forecast(Guid regionId, int? days, CancellationToken cancellationToken)
    {
        var period = days ?? _settings.ForecastDays;
        if (period < 1 || period > _settings.ForecastDays) throw new WeatherException(HttpStatusCode.BadRequest, $"Forecast days must be between 1 and {_settings.ForecastDays}.");
        var region = await Region(regionId, false, cancellationToken);
        var now = _time.GetUtcNow().UtcDateTime;
        var predictions = await _weather.GetForecast(regionId, region.BoundaryVersion, now, now.AddDays(period), cancellationToken);
        DateTime? fetchedAt = predictions.Count == 0 ? null : predictions.Min(item => item.FetchedAt);
        return new(regionId, Status(fetchedAt), fetchedAt, predictions.FirstOrDefault()?.Latitude, predictions.FirstOrDefault()?.Longitude, region.BoundaryVersion,
            predictions.Select(item => new WeatherHourlyResponse(item.ForecastAt, item.FetchedAt, item.Temperature, item.Humidity,
                item.ApparentTemperature, item.PrecipitationProbability, item.WindSpeed, item.CloudCover, item.UvIndex, item.SolarRadiation)).ToList());
    }
    public async Task<PagedResponse<WeatherCurrentResponse>> History(Guid regionId, GetWeatherHistoryRequest request, bool administrator, CancellationToken cancellationToken)
    {
        WeatherValidator.History(request);
        var region = await Region(regionId, administrator, cancellationToken);
        var (items, total) = await _weather.GetHistory(regionId, request.StartDate, request.EndDate, request.Page, request.PageSize, cancellationToken);
        return new() { Items = items.Select(item => Response(region, item)).ToList(), Page = request.Page, PageSize = request.PageSize, TotalItems = total };
    }
    public async Task<WeatherOverviewResponse> Overview(CancellationToken cancellationToken)
    {
        var regions = (await _weather.GetOverview(cancellationToken)).Select(item => Response(item.Region, item.Snapshot)).ToList();
        var current = regions.Where(item => item.DataStatus == "CURRENT").ToList();
        return new(regions.Count, current.Count, regions.Count(item => item.DataStatus == "STALE"), regions.Count(item => item.DataStatus == "NO_DATA"),
            current.Select(item => item.Temperature).Average(), current.Select(item => item.Humidity).Average(), current.Select(item => item.ApparentTemperature).Average(), regions);
    }
}
