using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetSensorsOverview;

public sealed class GetInstitutionalSensorsOverviewUseCase : IGetInstitutionalSensorsOverviewUseCase
{
    private readonly InstitutionalDashboardQuery _query;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly ILogger<GetInstitutionalSensorsOverviewUseCase> _logger;

    public GetInstitutionalSensorsOverviewUseCase(InstitutionalDashboardQuery query, IInstitutionalDashboardRepository dashboard, ILogger<GetInstitutionalSensorsOverviewUseCase> logger)
    {
        _query = query;
        _dashboard = dashboard;
        _logger = logger;
    }

    public async Task<InstitutionalSensorsResponse> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            /// :: Validate filters and access.
            var query = await _query.Prepare(actorId, request, null, false, cancellationToken);
            var sensors = await _dashboard.GetSensorsOverview(query, cancellationToken);
            var regions = await _dashboard.GetRegions(query, false, cancellationToken);

            return new InstitutionalSensorsResponse
            {
                TotalSensors = sensors.TotalSensors,
                ActiveSensors = sensors.ActiveSensors,
                InactiveSensors = sensors.InactiveSensors,
                MaintenanceSensors = sensors.MaintenanceSensors,
                SensorsWithCurrentData = sensors.SensorsWithCurrentData,
                SensorsWithStaleData = sensors.SensorsWithStaleData,
                SensorsWithoutReadings = sensors.SensorsWithoutReadings,
                LastSimulatedCommunication = sensors.LastSimulatedCommunication,
                Regions = regions.Items.Select(region => new InstitutionalRegionSensorResponse(region.RegionId, region.Name, region.SensorCount)).ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                TotalRegions = regions.Total
            };
        }
        finally
        {
            _logger.LogInformation("Institutional GetSensorsOverview for {OrganizationId} completed in {DurationMs} ms", request.OrganizationId, timer.ElapsedMilliseconds);
        }
    }
}
