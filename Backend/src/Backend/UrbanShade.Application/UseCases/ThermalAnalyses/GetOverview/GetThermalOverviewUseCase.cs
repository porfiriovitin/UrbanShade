using UrbanShade.Application.UseCases.ThermalAnalyses.GetMap;
using UrbanShade.Communication.Responses.ThermalAnalyses;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Application.UseCases.ThermalAnalyses.GetOverview;

public sealed class GetThermalOverviewUseCase : IGetThermalOverviewUseCase
{
    private readonly IGetThermalMapUseCase _map;

    public GetThermalOverviewUseCase(IGetThermalMapUseCase map)
    {
        _map = map;
    }

    public Task<ThermalOverviewResponse> Execute(CancellationToken cancellationToken = default) => Execute(null, null, null, cancellationToken);

    public async Task<ThermalOverviewResponse> Execute(Guid? cityId, Guid? organizationId, Guid? actorId, CancellationToken cancellationToken = default)
    {
        var map = await _map.Execute(cityId, organizationId, actorId, cancellationToken);
        var current = map.Sensors.Where(sensor => sensor.DataStatus == nameof(ThermalDataStatus.CURRENT)).ToList();

        return new ThermalOverviewResponse
        {
            TotalActiveSensors = map.Sensors.Count,
            SensorsWithCurrentData = current.Count,
            AverageTemperature = current.Count == 0 ? null : decimal.Round(current.Average(sensor => sensor.Temperature!.Value), 2, MidpointRounding.AwayFromZero),
            AverageHeatIndex = current.Count == 0 ? null : decimal.Round(current.Average(sensor => sensor.HeatIndex!.Value), 2, MidpointRounding.AwayFromZero),
            HighestHeatIndex = current.Count == 0 ? null : current.Max(sensor => sensor.HeatIndex),
            RiskDistribution = new ThermalRiskDistributionResponse
            {
                Low = current.Count(sensor => sensor.RiskLevel == nameof(ThermalRiskLevel.LOW)),
                Moderate = current.Count(sensor => sensor.RiskLevel == nameof(ThermalRiskLevel.MODERATE)),
                High = current.Count(sensor => sensor.RiskLevel == nameof(ThermalRiskLevel.HIGH)),
                Critical = current.Count(sensor => sensor.RiskLevel == nameof(ThermalRiskLevel.CRITICAL))
            }
        };
    }
}
