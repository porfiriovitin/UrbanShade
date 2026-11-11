namespace UrbanShade.Communication.Responses.ThermalAnalyses;

public sealed class ThermalMapResponse
{
    public List<ThermalMapSensorResponse> Sensors { get; init; } = [];
}

public sealed class ThermalMapSensorResponse : ThermalAnalysisResponse
{
    public Guid? CityId { get; init; }
    public Guid? OrganizationId { get; init; }
    public string SensorName { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
