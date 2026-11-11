namespace UrbanShade.Domain.Routing;

public sealed class RoutingSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://api.heigit.org/openrouteservice/v2/";
    public string Profile { get; set; } = "foot-walking";
    public int MaxAlternativeRoutes { get; set; } = 3;
    public bool GreenRoutesEnabled { get; set; } = true;
    public double MaximumWalkingDistanceMeters { get; set; } = 10000;
    public double MaximumSnapDistanceMeters { get; set; } = 250;
    public decimal MinimumThermalCoveragePercent { get; set; } = 70;
    public decimal MinimumExposureImprovement { get; set; } = 5;
    public decimal MaximumDistanceIncreasePercent { get; set; } = 25;
    public double MaximumAdditionalDurationMinutes { get; set; } = 8;
    public int CacheDurationMinutes { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 20;
    public int MaxRetryAttempts { get; set; } = 2;
    public int MaximumGeometryPoints { get; set; } = 5000;
    public int MaximumSegments { get; set; } = 20000;
    public int MaximumResponseBytes { get; set; } = 2000000;
    public double DuplicateToleranceMeters { get; set; } = 10;
    public decimal DuplicateOverlapPercent { get; set; } = 98;

    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Host != "api.heigit.org" || !uri.IsDefaultPort || uri.Fragment.Length > 0 || uri.AbsolutePath.TrimEnd('/') != "/openrouteservice/v2" || uri.Query.Length > 0 || uri.UserInfo.Length > 0 ||
            Profile != "foot-walking" || MaxAlternativeRoutes is < 1 or > 3 ||
            !double.IsFinite(MaximumWalkingDistanceMeters) || MaximumWalkingDistanceMeters is <= 0 or > 100000 ||
            !double.IsFinite(MaximumSnapDistanceMeters) || MaximumSnapDistanceMeters is <= 0 or > 1000 ||
            MinimumThermalCoveragePercent is <= 0 or > 100 || MinimumExposureImprovement is <= 0 or > 100 ||
            MaximumDistanceIncreasePercent is < 0 or > 100 || !double.IsFinite(MaximumAdditionalDurationMinutes) || MaximumAdditionalDurationMinutes is < 0 or > 120 ||
            CacheDurationMinutes is < 0 or > 60 || TimeoutSeconds is < 1 or > 120 || MaxRetryAttempts is < 0 or > 3 ||
            MaximumGeometryPoints is < 2 or > 20000 || MaximumSegments is < 1 or > 100000 || MaximumResponseBytes is < 1000 or > 10000000 ||
            !double.IsFinite(DuplicateToleranceMeters) || DuplicateToleranceMeters is <= 0 or > 100 || DuplicateOverlapPercent is < 90 or > 100)
            throw new InvalidOperationException("Invalid routing settings.");
    }
}
