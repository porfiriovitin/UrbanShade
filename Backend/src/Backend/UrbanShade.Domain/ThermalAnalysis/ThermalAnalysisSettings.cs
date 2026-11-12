using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.ThermalAnalysis;

public sealed class ThermalAnalysisSettings
{
    public bool Enabled { get; set; } = true;
    public string AlgorithmVersion { get; set; } = "1.0";
    public double StaleDataThresholdMinutes { get; set; } = 15;
    public ThermalRiskThresholds RiskThresholds { get; set; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AlgorithmVersion) || AlgorithmVersion.Length > 32 || AlgorithmVersion != AlgorithmVersion.Trim())
            throw new InvalidOperationException("ThermalAnalysis:AlgorithmVersion must contain between 1 and 32 characters without surrounding whitespace.");
        if (!double.IsFinite(StaleDataThresholdMinutes) || StaleDataThresholdMinutes <= 0 || StaleDataThresholdMinutes > 525600)
            throw new InvalidOperationException("ThermalAnalysis:StaleDataThresholdMinutes must be greater than zero and at most 525600 minutes.");
        if (RiskThresholds is null || RiskThresholds.Moderate >= RiskThresholds.High || RiskThresholds.High >= RiskThresholds.Critical)
            throw new InvalidOperationException("ThermalAnalysis risk thresholds must be ordered: Moderate < High < Critical, in Celsius.");
    }

    public ThermalRiskLevel Classify(decimal heatIndex)
    {
        if (heatIndex >= RiskThresholds.Critical) return ThermalRiskLevel.CRITICAL;
        if (heatIndex >= RiskThresholds.High) return ThermalRiskLevel.HIGH;
        if (heatIndex >= RiskThresholds.Moderate) return ThermalRiskLevel.MODERATE;

        return ThermalRiskLevel.LOW;
    }
}

public sealed class ThermalRiskThresholds
{
    public decimal Moderate { get; set; } = 27;
    public decimal High { get; set; } = 32;
    public decimal Critical { get; set; } = 39;
}
