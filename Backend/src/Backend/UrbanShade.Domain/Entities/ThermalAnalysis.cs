using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Entities;

public class ThermalAnalysis : EntityBase
{
    private ThermalAnalysis() { }

    public ThermalAnalysis(Guid sensorReadingId, decimal heatIndex, ThermalRiskLevel riskLevel, string algorithmVersion, DateTime analyzedAt)
    {
        if (sensorReadingId == Guid.Empty) throw new ArgumentException("A sensor reading is required.", nameof(sensorReadingId));
        if (!Enum.IsDefined(riskLevel)) throw new ArgumentOutOfRangeException(nameof(riskLevel));
        if (string.IsNullOrWhiteSpace(algorithmVersion) || algorithmVersion.Length > 32)
            throw new ArgumentException("An algorithm version with at most 32 characters is required.", nameof(algorithmVersion));
        if (analyzedAt.Kind != DateTimeKind.Utc) throw new ArgumentException("AnalyzedAt must be UTC.", nameof(analyzedAt));

        SensorReadingId = sensorReadingId;
        HeatIndex = heatIndex;
        RiskLevel = riskLevel;
        AlgorithmVersion = algorithmVersion;
        AnalyzedAt = analyzedAt;
    }

    public Guid SensorReadingId { get; private set; }
    public decimal HeatIndex { get; private set; }
    public ThermalRiskLevel RiskLevel { get; private set; }
    public string AlgorithmVersion { get; private set; } = string.Empty;
    public DateTime AnalyzedAt { get; private set; }
}
