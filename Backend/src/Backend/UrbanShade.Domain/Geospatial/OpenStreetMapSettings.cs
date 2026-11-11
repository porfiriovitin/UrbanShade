namespace UrbanShade.Domain.Geospatial;

public sealed class OpenStreetMapSettings
{
    public bool Enabled { get; set; }
    public bool AutomaticRefreshEnabled { get; set; }
    public int RefreshIntervalDays { get; set; } = 7;
    public int MaximumElements { get; set; } = 2000;
    public int MaximumVertices { get; set; } = 20000;
    public int MaximumResponseBytes { get; set; } = 16 * 1024 * 1024;
    public OpenStreetMapParameters Parameters() => new(MaximumElements, MaximumVertices, MaximumResponseBytes);
    public void Validate()
    {
        if (RefreshIntervalDays < 1 || MaximumElements is < 1 or > 10000 || MaximumVertices is < 4 or > 100000 || MaximumResponseBytes is < 1024 or > 32 * 1024 * 1024)
            throw new InvalidOperationException("Invalid OpenStreetMap import settings.");
    }
}
