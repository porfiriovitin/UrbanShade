using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Weather;

namespace UrbanShade.Domain.Repositories;

public interface IWeatherRepository
{
    Task<IWeatherRefreshLease?> AcquireRefresh(Guid regionId, CancellationToken cancellationToken);

    Task<bool> RequestRefresh(Guid regionId, Guid leaseId, int boundaryVersion, DateTime cutoff, DateTime now, CancellationToken cancellationToken);

    Task DeferRefresh(Guid regionId, Guid leaseId, DateTime retryAfter, CancellationToken cancellationToken);

    Task<List<Guid>> GetDueRegions(DateTime now, CancellationToken cancellationToken);

    Task<WeatherSnapshot?> GetLatest(Guid regionId, int boundaryVersion, CancellationToken cancellationToken);

    Task<List<WeatherForecast>> GetForecast(Guid regionId, int boundaryVersion, DateTime start, DateTime end, CancellationToken cancellationToken);

    Task<(List<WeatherSnapshot> Items, int Total)> GetHistory(Guid regionId, DateTime? start, DateTime? end, int page, int pageSize, CancellationToken cancellationToken);

    Task<List<(WeatherRegionSummary Region, WeatherSnapshot? Snapshot)>> GetOverview(CancellationToken cancellationToken, Guid? cityId = null);

    Task<bool> Save(GeoRegion region, Guid leaseId, double latitude, double longitude, WeatherObservation observation, CancellationToken cancellationToken);
}

public interface IWeatherRefreshLease : IAsyncDisposable
{
    Guid Id { get; }
}

public sealed record WeatherRegionSummary(Guid Id, string Name);
