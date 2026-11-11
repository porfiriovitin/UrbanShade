using System.Text.Json;

namespace UrbanShade.Infraestructure.Geospatial;

public sealed class GoogleEarthEngineOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://earthengine.googleapis.com/v1/";
    public string SentinelDataset { get; set; } = "COPERNICUS/S2_SR_HARMONIZED";
    public string CloudProbabilityDataset { get; set; } = "COPERNICUS/S2_CLOUD_PROBABILITY";
    public string Landsat8Dataset { get; set; } = "LANDSAT/LC08/C02/T1_L2";
    public string Landsat9Dataset { get; set; } = "LANDSAT/LC09/C02/T1_L2";
    public Dictionary<string, string> Credentials { get; set; } = [];
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId) || ProjectId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new InvalidOperationException("A valid Earth Engine project is required.");
        if (BaseUrl.TrimEnd('/') != "https://earthengine.googleapis.com/v1" || SentinelDataset != "COPERNICUS/S2_SR_HARMONIZED" ||
            CloudProbabilityDataset != "COPERNICUS/S2_CLOUD_PROBABILITY" || Landsat8Dataset != "LANDSAT/LC08/C02/T1_L2" || Landsat9Dataset != "LANDSAT/LC09/C02/T1_L2")
            throw new InvalidOperationException("Unsupported Earth Engine endpoint or dataset.");
    }
}
