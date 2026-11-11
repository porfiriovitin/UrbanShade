using System.Text.Json;

namespace UrbanShade.Communication.Requests.Geo;

public sealed class RegisterGeoRegionRequest
{
    public Guid CityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonElement Boundary { get; set; }
}
public sealed class UpdateGeoRegionRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public JsonElement? Boundary { get; set; }
}
public sealed class RefreshGeoRegionRequest
{
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
}
