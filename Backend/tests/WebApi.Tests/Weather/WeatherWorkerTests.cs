using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Weather;
using UrbanShade.Infraestructure.DataAcess;
using UrbanShade.Infraestructure.Workers;

namespace WebApi.Tests.Weather;

public sealed class WeatherWorkerTests : BaseIntegrationTest
{
    public WeatherWorkerTests(UrbanShadeApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Worker_IsolatesRegionFailures_RespectsRefreshInterval_AndRetriesDueRegions()
    {
        var provider = Factory.Services.GetRequiredService<WeatherTestProvider>();
        provider.Reset();
        provider.FailNext = 1;
        var ids = new List<Guid>();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            var city = new City("Worker test city", "SP", "3500000");
            context.Cities.Add(city);
            for (var index = 0; index < 2; index++)
            {
                var polygon = new Polygon(new LinearRing([new(-46.66, -23.59), new(-46.65, -23.59), new(-46.65, -23.58), new(-46.66, -23.59)])) { SRID = 4326 };
                var region = new GeoRegion(city.Id, $"Worker region {index}", null, polygon);
                context.GeoRegions.Add(region);
                ids.Add(region.Id);
            }
            await context.SaveChangesAsync();
        }
        using var worker = new WeatherBackgroundService(Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OpenMeteoSettings()), TimeProvider.System, NullLogger<WeatherBackgroundService>.Instance);
        await worker.RefreshDueRegions(default);
        Assert.Equal(2, provider.Calls);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
            Assert.Equal(1, await context.WeatherSnapshots.CountAsync(snapshot => ids.Contains(snapshot.GeoRegionId)));
            Assert.Equal(2, await context.GeoRegions.CountAsync(region => ids.Contains(region.Id) && region.WeatherRequestedAt.HasValue));
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<IWeatherRepository>().GetDueRegions(DateTime.UtcNow, default));
            await worker.RefreshDueRegions(default);
            Assert.Equal(2, provider.Calls);
            await context.GeoRegions.Where(region => ids.Contains(region.Id) && !context.WeatherSnapshots.Any(snapshot => snapshot.GeoRegionId == region.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(region => region.WeatherRequestedAt, DateTime.UtcNow.AddMinutes(-6)));
        }
        await worker.RefreshDueRegions(default);
        Assert.Equal(3, provider.Calls);
        await using var verification = Factory.Services.CreateAsyncScope();
        Assert.Equal(2, await verification.ServiceProvider.GetRequiredService<UrbanShadeDbContext>().WeatherSnapshots.CountAsync(snapshot => ids.Contains(snapshot.GeoRegionId)));
    }
}
