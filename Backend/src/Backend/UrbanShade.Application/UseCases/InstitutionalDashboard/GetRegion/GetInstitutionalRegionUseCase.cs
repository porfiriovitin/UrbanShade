using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetRegion;

public sealed class GetInstitutionalRegionUseCase : IGetInstitutionalRegionUseCase
{
    private readonly InstitutionalDashboardQuery _query;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly ILogger<GetInstitutionalRegionUseCase> _logger;

    public GetInstitutionalRegionUseCase(InstitutionalDashboardQuery query, IInstitutionalDashboardRepository dashboard, ILogger<GetInstitutionalRegionUseCase> logger)
    {
        _query = query;
        _dashboard = dashboard;
        _logger = logger;
    }

    public async Task<InstitutionalRegionResponse> Execute(Guid actorId, Guid regionId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            /// :: Validate filters and access.
            var query = await _query.Prepare(actorId, request, regionId, false, cancellationToken);
            var data = await _dashboard.GetRegions(query with { Page = 1, PageSize = 1 }, false, cancellationToken);
            var region = data.Items.SingleOrDefault() ?? throw new InstitutionalDashboardException(ErrorKind.NotFound, "Region is unavailable.");
            var sensors = await _dashboard.GetSensorsOverview(query, cancellationToken);

            return _query.ToResponse(region, true)with
            {
                AverageTemperature = new(sensors.AverageTemperature, sensors.TemperatureSampleCount)
            };
        }
        finally
        {
            _logger.LogInformation("Institutional GetRegion for {OrganizationId} completed in {DurationMs} ms", request.OrganizationId, timer.ElapsedMilliseconds);
        }
    }
}
