using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Weather;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class WeatherRepository : IWeatherRepository
{
    private readonly UrbanShadeDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly OpenMeteoSettings _settings;
    public WeatherRepository(UrbanShadeDbContext context, IMemoryCache cache, IOptions<OpenMeteoSettings> settings)
    { 
        _context = context; 
        _cache = cache; 
        _settings = settings.Value; 
    }

    public async Task<IAsyncDisposable?> AcquireRefresh(Guid regionId, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        try
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(hashtextextended(@key, 0))", connection);
            command.Parameters.AddWithValue("key", $"weather:{regionId}");
            if ((bool)(await command.ExecuteScalarAsync(cancellationToken))!) return new RefreshLock(_context, regionId);
            await _context.Database.CloseConnectionAsync();
            return null;
        }
        catch { await _context.Database.CloseConnectionAsync(); throw; }
    }

    public async Task<bool> RequestRefresh(Guid regionId, int boundaryVersion, DateTime cutoff, DateTime now, CancellationToken cancellationToken) =>
        await _context.GeoRegions.Where(region => region.Id == regionId && region.Active && region.BoundaryVersion == boundaryVersion &&
            (!region.WeatherRetryAfter.HasValue || region.WeatherRetryAfter <= now) &&
            (!region.WeatherRequestedAt.HasValue || region.WeatherRequestedAt <= cutoff))
            .ExecuteUpdateAsync(setters => setters.SetProperty(region => region.WeatherRequestedAt, now)
                .SetProperty(region => region.WeatherRetryAfter, (DateTime?)null), cancellationToken) == 1;

    public Task<List<Guid>> GetDueRegions(DateTime now, CancellationToken cancellationToken) =>
        _context.GeoRegions.AsNoTracking().Where(region => region.Active && (!region.WeatherRequestedAt.HasValue || region.WeatherRequestedAt <= now.AddMinutes(-_settings.MinimumRefreshIntervalMinutes)) &&
            (!region.WeatherRetryAfter.HasValue || region.WeatherRetryAfter <= now) &&
            !_context.WeatherSnapshots.Any(snapshot => snapshot.GeoRegionId == region.Id && snapshot.BoundaryVersion == region.BoundaryVersion && snapshot.FetchedAt > now.AddMinutes(-_settings.RefreshIntervalMinutes)))
            .OrderBy(region => region.WeatherRequestedAt).ThenBy(region => region.Id).Take(50).Select(region => region.Id).ToListAsync(cancellationToken);

    public async Task DeferRefresh(Guid regionId, DateTime retryAfter, CancellationToken cancellationToken) =>
        await _context.GeoRegions.Where(region => region.Id == regionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(region => region.WeatherRetryAfter, retryAfter), cancellationToken);

    public async Task<WeatherSnapshot?> GetLatest(Guid regionId, int boundaryVersion, CancellationToken cancellationToken)
    {
        var key = $"weather:current:{regionId}:{boundaryVersion}";
        if (_cache.TryGetValue<WeatherSnapshot>(key, out var cached)) return cached;
        var snapshot = await _context.WeatherSnapshots.AsNoTracking().Where(snapshot => snapshot.GeoRegionId == regionId && snapshot.BoundaryVersion == boundaryVersion)
            .OrderByDescending(snapshot => snapshot.FetchedAt).ThenByDescending(snapshot => snapshot.ObservedAt).FirstOrDefaultAsync(cancellationToken);
        if (snapshot is not null && _settings.CacheDurationMinutes > 0) _cache.Set(key, snapshot, TimeSpan.FromMinutes(_settings.CacheDurationMinutes));
        return snapshot;
    }

    public async Task<List<WeatherForecast>> GetForecast(Guid regionId, int boundaryVersion, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var key = $"weather:forecast:{regionId}:{boundaryVersion}";
        if (!_cache.TryGetValue<List<WeatherForecast>>(key, out var forecasts))
        {
            forecasts = await _context.WeatherForecasts.AsNoTracking().Where(item => item.GeoRegionId == regionId && item.BoundaryVersion == boundaryVersion && item.ForecastAt > start).OrderBy(item => item.ForecastAt).ToListAsync(cancellationToken);

            if (forecasts.Count > 0 && _settings.CacheDurationMinutes > 0) 
                _cache.Set(key, forecasts, TimeSpan.FromMinutes(_settings.CacheDurationMinutes));
        }
        return forecasts!.Where(item => item.ForecastAt > start && item.ForecastAt <= end).ToList();
    }

    public async Task<(List<WeatherSnapshot> Items, int Total)> GetHistory(Guid regionId, DateTime? start, DateTime? end, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.WeatherSnapshots.AsNoTracking().Where(snapshot => snapshot.GeoRegionId == regionId &&
            (!start.HasValue || snapshot.ObservedAt >= start) && (!end.HasValue || snapshot.ObservedAt <= end));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(snapshot => snapshot.ObservedAt).ThenByDescending(snapshot => snapshot.FetchedAt).ThenBy(snapshot => snapshot.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<List<(GeoRegion Region, WeatherSnapshot? Snapshot)>> GetOverview(CancellationToken cancellationToken)
    {
        var rows = await _context.GeoRegions.AsNoTracking().Where(region => region.Active).OrderBy(region => region.Name).ThenBy(region => region.Id)
            .Select(region => new
            {
                Region = region,
                Snapshot = _context.WeatherSnapshots.Where(snapshot => snapshot.GeoRegionId == region.Id && snapshot.BoundaryVersion == region.BoundaryVersion)
                    .OrderByDescending(snapshot => snapshot.FetchedAt).ThenByDescending(snapshot => snapshot.ObservedAt).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        return rows.Select(row => (row.Region, row.Snapshot)).ToList();
    }

    public async Task<bool> Save(GeoRegion region, double latitude, double longitude, WeatherObservation observation, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        /// :: Recheck the polygon under a short row lock after the HTTP request has finished.
        var current = await _context.GeoRegions.FromSqlInterpolated($"SELECT * FROM \"GeoRegions\" WHERE \"Id\" = {region.Id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(cancellationToken);
       
        if (current is null || !current.Active || current.BoundaryVersion != region.BoundaryVersion) 
            return false;

        var predictions = observation.Hourly.Where(item => item.ForecastAt > observation.FetchedAt).ToList();
        var times = predictions.Select(item => item.ForecastAt).ToList();
        var existing = await _context.WeatherForecasts.Where(item => item.GeoRegionId == region.Id && item.Source == "OPEN_METEO" && times.Contains(item.ForecastAt)) .ToDictionaryAsync(item => item.ForecastAt, cancellationToken);
        
        foreach (var prediction in predictions)
        {
            if (existing.TryGetValue(prediction.ForecastAt, out var forecast)) forecast.Update(region.BoundaryVersion, latitude, longitude, prediction, observation.FetchedAt);
            else _context.WeatherForecasts.Add(new(region.Id, region.BoundaryVersion, latitude, longitude, prediction, observation.FetchedAt));
        }

        _context.WeatherSnapshots.Add(new(region.Id, region.BoundaryVersion, latitude, longitude, observation));
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _cache.Remove($"weather:current:{region.Id}:{region.BoundaryVersion}");
        _cache.Remove($"weather:forecast:{region.Id}:{region.BoundaryVersion}");
        return true;
    }

    private sealed class RefreshLock(UrbanShadeDbContext context, Guid regionId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtextextended(@key, 0))", (NpgsqlConnection)context.Database.GetDbConnection()) { CommandTimeout = 5 };
                command.Parameters.AddWithValue("key", $"weather:{regionId}");
                await command.ExecuteScalarAsync(CancellationToken.None);
            }
            finally { await context.Database.CloseConnectionAsync(); }
        }
    }
}
