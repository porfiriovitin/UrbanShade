namespace UrbanShade.Domain.Routing;

public sealed class RouteRecommendationCalculator
{
    private readonly RoutingSettings _settings;

    public RouteRecommendationCalculator(RoutingSettings settings)
    {
        _settings = settings;
    }

    public RouteRecommendation Recommend(IReadOnlyList<RouteExposure> routes)
    {
        var conventional = routes.Single(item => item.Route.IsConventional);
        if (routes.Count == 1) return new(conventional.Route.Id, "SINGLE_ROUTE_AVAILABLE");
        if (!conventional.CanRecommendThermally) return new(conventional.Route.Id, "INSUFFICIENT_THERMAL_DATA");

        var comparable = routes.Where(item => !item.Route.IsConventional && item.CanRecommendThermally && item.Composition == conventional.Composition && item.ThermalCoveragePercent >= conventional.ThermalCoveragePercent).ToList();
        if (comparable.Count == 0) return new(conventional.Route.Id, "INSUFFICIENT_THERMAL_DATA");

        var best = comparable.Where(item => conventional.ExposureIndex - item.ExposureIndex >= _settings.MinimumExposureImprovement && item.Route.DistanceMeters <= conventional.Route.DistanceMeters * (1 + (double)_settings.MaximumDistanceIncreasePercent / 100) && item.Route.DurationSeconds - conventional.Route.DurationSeconds <= _settings.MaximumAdditionalDurationMinutes * 60).OrderBy(item => item.ExposureIndex).ThenBy(item => item.HighExposureDistanceMeters).ThenBy(item => item.Route.DurationSeconds).ThenBy(item => item.Route.DistanceMeters).ThenBy(item => item.Route.Id, StringComparer.Ordinal).FirstOrDefault();

        return best is null ? new(conventional.Route.Id, "NO_BETTER_ALTERNATIVE") : new(best.Route.Id, "LOWER_HEAT_EXPOSURE");
    }
}
