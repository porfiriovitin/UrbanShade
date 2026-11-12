namespace UrbanShade.Domain.InstitutionalDashboard;

public static class InterventionPriority
{
    public static string Calculate(bool current, string level, decimal? vegetation, decimal? anomaly, bool? heatIsland, InstitutionalDashboardSettings settings)
    {
        if (!current) return "UNDETERMINED";
        var adverse = vegetation < settings.LowVegetationCoveragePercent || anomaly >= settings.PrioritySurfaceAnomaly || heatIsland == true;
        if (level == "CRITICAL" && adverse) return "HIGH";
        return level is "CRITICAL" or "HIGH" || adverse ? "MEDIUM" : "LOW";
    }
}
