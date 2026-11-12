using System.Text.Json.Serialization;

namespace UrbanShade.Communication.Requests.Routes;

public sealed class CompareRoutesRequest
{
    public RoutePointRequest Origin { get; set; } = null!;
    public RoutePointRequest Destination { get; set; } = null!;
    public string TravelMode { get; set; } = "WALKING";
}

public sealed class RoutePointRequest
{
    [JsonRequired]
    public double Latitude { get; set; }

    [JsonRequired]
    public double Longitude { get; set; }
}
