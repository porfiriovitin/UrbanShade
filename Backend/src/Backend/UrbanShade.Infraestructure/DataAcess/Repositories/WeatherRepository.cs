using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Weather;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class WeatherRepository : IWeatherRepository
{
    private readonly UrbanShadeDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly WeatherSettings _settings;

    public WeatherRepository(UrbanShadeDbContext context, IMemoryCache cache, IOptions<WeatherSettings> settings)
    {
        _context = context;
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<IWeatherRefreshLease?> AcquireRefresh(Guid regionId, CancellationToken cancellationToken)
    {
        var leaseId = Guid.NewGuid();

        /// :: Release the connection before calling the provider.
        var changed = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "GeoRegions" SET "WeatherLeaseId" = {leaseId}, "WeatherLeaseExpiresAt" = now() + interval '10 minutes'
            WHERE "Id" = {regionId} AND ("WeatherLeaseExpiresAt" IS NULL OR "WeatherLeaseExpiresAt" <= now())
            """, cancellationToken);

        return changed == 1 ? new RefreshLease(_context, regionId, leaseId) : null;
    }

    public async Task<bool> RequestRefresh(Guid regionId, Guid leaseId, int boundaryVersion, DateTime cutoff, DateTime now, CancellationToken cancellationToken) => await _context.GeoRegions.Where(region => region.Id == regionId && region.WeatherLeaseId == leaseId && region.WeatherLeaseExpiresAt > DateTime.UtcNow && region.Active && region.BoundaryVersion == boundaryVersion && (!region.WeatherRetryAfter.HasValue || region.WeatherRetryAfter <= now) && (!region.WeatherRequestedAt.HasValue || region.WeatherRequestedAt <= cutoff)).ExecuteUpdateAsync(setters => setters.SetProperty(region => region.WeatherRequestedAt, now).SetProperty(region => region.WeatherRetryAfter, (DateTime?)null), cancellationToken) == 1;

    public Task<List<Guid>> GetDueRegions(DateTime now, CancellationToken cancellationToken) => _context.GeoRegions.AsNoTracking().Where(region => region.Active && (!region.WeatherRequestedAt.HasValue || region.WeatherRequestedAt <= now.AddMinutes(-_settings.MinimumRefreshIntervalMinutes)) && (!region.WeatherRetryAfter.HasValue || region.WeatherRetryAfter <= now) && !_context.WeatherSnapshots.Any(snapshot => snapshot.GeoRegionId == region.Id && snapshot.BoundaryVersion == region.BoundaryVersion && snapshot.FetchedAt > now.AddMinutes(-_settings.RefreshIntervalMinutes))).OrderBy(region => region.WeatherRequestedAt).ThenBy(region => region.Id).Take(50).Select(region => region.Id).ToListAsync(cancellationToken);

    public async Task DeferRefresh(Guid regionId, Guid leaseId, DateTime retryAfter, CancellationToken cancellationToken) => await _context.GeoRegions.Where(region => region.Id == regionId && region.WeatherLeaseId == leaseId && region.WeatherLeaseExpiresAt > DateTime.UtcNow).ExecuteUpdateAsync(setters => setters.SetProperty(region => region.WeatherRetryAfter, retryAfter), cancellationToken);

    public async Task<WeatherSnapshot?> GetLatest(Guid regionId, int boundaryVersion, CancellationToken cancellationToken)
    {
        var key = $"weather:current:{regionId}:{boundaryVersion}";
        if (_cache.TryGetValue<WeatherSnapshot>(key, out var cached)) return cached;

        var snapshot = await _context.WeatherSnapshots.AsNoTracking().Where(snapshot => snapshot.GeoRegionId == regionId && snapshot.BoundaryVersion == boundaryVersion).OrderByDescending(snapshot => snapshot.FetchedAt).ThenByDescending(snapshot => snapshot.ObservedAt).FirstOrDefaultAsync(cancellationToken);
        if (snapshot is not null && _settings.CacheDurationMinutes > 0)
            _cache.Set(key, snapshot, TimeSpan.FromMinutes(_settings.CacheDurationMinutes));

        return snapshot;
    }

    public async Task<List<WeatherForecast>> GetForecast(Guid regionId, int boundaryVersion, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var key = $"weather:forecast:{regionId}:{boundaryVersion}";
        if (!_cache.TryGetValue<List<WeatherForecast>>(key, out var forecasts))
        {
            forecasts = await _context.WeatherForecasts.AsNoTracking().Where(item => item.GeoRegionId == regionId && item.BoundaryVersion == boundaryVersion && item.ForecastAt > start && item.Source == _context.WeatherSnapshots.Where(snapshot => snapshot.GeoRegionId == regionId && snapshot.BoundaryVersion == boundaryVersion).OrderByDescending(snapshot => snapshot.FetchedAt).ThenByDescending(snapshot => snapshot.ObservedAt).Select(snapshot => snapshot.Source).First()).OrderBy(item => item.ForecastAt).ToListAsync(cancellationToken);
            if (forecasts.Count > 0 && _settings.CacheDurationMinutes > 0)
                _cache.Set(key, forecasts, TimeSpan.FromMinutes(_settings.CacheDurationMinutes));
        }

        return forecasts!.Where(item => item.ForecastAt > start && item.ForecastAt <= end).ToList();
    }

    public async Task<(List<WeatherSnapshot> Items, int Total)> GetHistory(Guid regionId, DateTime? start, DateTime? end, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.WeatherSnapshots.AsNoTracking().Where(snapshot => snapshot.GeoRegionId == regionId && (!start.HasValue || snapshot.ObservedAt >= start) && (!end.HasValue || snapshot.ObservedAt <= end));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(snapshot => snapshot.ObservedAt).ThenByDescending(snapshot => snapshot.FetchedAt).ThenBy(snapshot => snapshot.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<List<(WeatherRegionSummary Region, WeatherSnapshot? Snapshot)>> GetOverview(CancellationToken cancellationToken, Guid? cityId = null)
    {
        var rows = await _context.GeoRegions.AsNoTracking().Where(region => region.Active && (!cityId.HasValue || region.CityId == cityId)).OrderBy(region => region.Name).ThenBy(region => region.Id).Select(region => new
        {
            Region = new WeatherRegionSummary(region.Id, region.Name),
            Snapshot = _context.WeatherSnapshots.Where(snapshot => snapshot.GeoRegionId == region.Id && snapshot.BoundaryVersion == region.BoundaryVersion).OrderByDescending(snapshot => snapshot.FetchedAt).ThenByDescending(snapshot => snapshot.ObservedAt).FirstOrDefault()
        }).Take(1001).ToListAsync(cancellationToken);
        if (rows.Count > 1000)
            throw new UrbanShade.Exceptions.WeatherException(UrbanShade.Exceptions.ErrorKind.Validation, "Overview exceeds 1000 regions. Select a smaller city scope.");

        return rows.Select(row => (row.Region, row.Snapshot)).ToList();
    }

    public async Task<bool> Save(GeoRegion region, Guid leaseId, double latitude, double longitude, WeatherObservation observation, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        /// :: Lock and recheck boundaries after the HTTP request.
        var current = await _context.GeoRegions.FromSqlInterpolated($"SELECT * FROM \"GeoRegions\" WHERE \"Id\" = {region.Id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (current is null || !current.Active || current.BoundaryVersion != region.BoundaryVersion || current.WeatherLeaseId != leaseId || !await _context.GeoRegions.AnyAsync(item => item.Id == region.Id && item.WeatherLeaseExpiresAt > DateTime.UtcNow, cancellationToken)) return false;

        var predictions = observation.Hourly.Where(item => item.ForecastAt > observation.FetchedAt).ToList();
        var times = predictions.Select(item => item.ForecastAt).ToList();
        var existing = await _context.WeatherForecasts.Where(item => item.GeoRegionId == region.Id && item.Source == observation.Source && times.Contains(item.ForecastAt)).ToDictionaryAsync(item => item.ForecastAt, cancellationToken);

        foreach (var prediction in predictions)
        {
            if (existing.TryGetValue(prediction.ForecastAt, out var forecast))
                forecast.Update(region.BoundaryVersion, latitude, longitude, prediction, observation.FetchedAt);
            else
                _context.WeatherForecasts.Add(new(region.Id, region.BoundaryVersion, latitude, longitude, prediction, observation.FetchedAt, observation.Source));
        }

        _context.WeatherSnapshots.Add(new(region.Id, region.BoundaryVersion, latitude, longitude, observation));
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _cache.Remove($"weather:current:{region.Id}:{region.BoundaryVersion}");
        _cache.Remove($"weather:forecast:{region.Id}:{region.BoundaryVersion}");

        return true;
    }

    private sealed class RefreshLease(UrbanShadeDbContext context, Guid regionId, Guid leaseId) : IWeatherRefreshLease
    {
        public Guid Id => leaseId;

        public async ValueTask DisposeAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await context.GeoRegions.Where(region => region.Id == regionId && region.WeatherLeaseId == leaseId).ExecuteUpdateAsync(update => update.SetProperty(region => region.WeatherLeaseId, (Guid?)null).SetProperty(region => region.WeatherLeaseExpiresAt, (DateTime?)null), timeout.Token);
        }
    }
}
