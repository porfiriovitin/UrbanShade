using System.Text.Json.Serialization;

namespace UrbanShade.Communication.Requests.Sensors;

public class UpdateSensorRequest
{
    private string? _code;
    private string? _name;
    private string? _description;
    private double? _latitude;
    private double? _longitude;
    private DateTime? _installedAt;

    public string? Code { get => _code; init { _code = value; HasCode = true; } }
    public string? Name { get => _name; init { _name = value; HasName = true; } }
    public string? Description { get => _description; init { _description = value; HasDescription = true; } }
    public double? Latitude { get => _latitude; init { _latitude = value; HasLatitude = true; } }
    public double? Longitude { get => _longitude; init { _longitude = value; HasLongitude = true; } }
    public DateTime? InstalledAt { get => _installedAt; init { _installedAt = value; HasInstalledAt = true; } }

    /// :: Track supplied fields to distinguish omitted values from explicit nulls.
    [JsonIgnore]
    public bool HasCode { get; private set; }
    [JsonIgnore]
    public bool HasName { get; private set; }
    [JsonIgnore]
    public bool HasDescription { get; private set; }
    [JsonIgnore]
    public bool HasLatitude { get; private set; }
    [JsonIgnore]
    public bool HasLongitude { get; private set; }
    [JsonIgnore]
    public bool HasInstalledAt { get; private set; }
}
