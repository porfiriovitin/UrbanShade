using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetOverview;

public sealed class GetInstitutionalOverviewUseCase : IGetInstitutionalOverviewUseCase
{
    private readonly InstitutionalDashboardQuery _query;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly TimeProvider _time;
    private readonly ILogger<GetInstitutionalOverviewUseCase> _logger;

    public GetInstitutionalOverviewUseCase(InstitutionalDashboardQuery query, IInstitutionalDashboardRepository dashboard,
        TimeProvider time, ILogger<GetInstitutionalOverviewUseCase> logger)
    {
        _query = query;
        _dashboard = dashboard;
        _time = time;
        _logger = logger;
    }

    public async Task<InstitutionalOverviewResponse> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            /// :: Validate filters and authorize the selected institutional scope before reading indicators.
            var query = await _query.Prepare(actorId, request, null, false, cancellationToken);
            var totals = await _dashboard.GetOverview(query, cancellationToken);
            var sensors = await _dashboard.GetSensorsOverview(query, cancellationToken);
            var environment = await _dashboard.GetEnvironment(query, cancellationToken);
            return new InstitutionalOverviewResponse
            {
                OrganizationId = query.OrganizationId, TotalRegions = totals.TotalRegions,
                TotalSensors = sensors.TotalSensors, ActiveSensors = sensors.ActiveSensors,
                InactiveSensors = sensors.InactiveSensors, MaintenanceSensors = sensors.MaintenanceSensors,
                AverageTemperature = new(sensors.AverageTemperature, sensors.TemperatureSampleCount),
                AverageHeatIndex = new(sensors.AverageHeatIndex, sensors.HeatIndexSampleCount),
                AverageExposureIndex = new(totals.AverageExposureIndex, totals.ExposureSampleCount),
                AverageVegetationCoveragePercent = environment.AverageVegetationCoveragePercent,
                VegetationSampleCount = environment.VegetationSampleCount, VegetationSampleAreaSquareMeters = environment.VegetationSampleAreaSquareMeters,
                CriticalRegions = totals.Critical, HeatIslandCandidates = totals.HeatIslandCandidates,
                RegionsWithValidAnalysis = totals.RegionsWithValidAnalysis, StaleRegions = totals.StaleRegions,
                RegionsWithInsufficientData = totals.InsufficientData,
                AnalysisMode = totals.HasSimulatedData || sensors.TemperatureSampleCount > 0 ? "DEMONSTRATION" : "EXPERIMENTAL",
                CalculatedAt = _time.GetUtcNow().UtcDateTime
            };

        }
        finally
        {
            _logger.LogInformation("Institutional GetOverview for {OrganizationId} completed in {DurationMs} ms", request.OrganizationId, timer.ElapsedMilliseconds);
        }
    }
}
