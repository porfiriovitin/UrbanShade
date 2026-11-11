using System.Text.Json;

namespace UrbanShade.Communication.Responses.RegionThermalAnalyses;

public sealed class RegionThermalHistoryResponse
{
    public List<RegionThermalAnalysisResponse> Items { get; init; } = [];
    public int TotalItems { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class RegionThermalComparisonResponse
{
    public List<RegionThermalAnalysisResponse> Items { get; init; } = [];
    public bool HasComparisonLimitations { get; init; }
    public string Note { get; init; } = "Experimental index. Compare source dates, composition and operational quality.";
}

public sealed class RegionThermalMapResponse
{
    public string Type { get; init; } = "FeatureCollection";
    public List<RegionThermalMapFeature> Features { get; init; } = [];
}

public sealed class RegionThermalMapFeature
{
    public string Type { get; init; } = "Feature";
    public JsonElement Geometry { get; init; }
    public RegionThermalAnalysisResponse Properties { get; init; } = null!;
}
