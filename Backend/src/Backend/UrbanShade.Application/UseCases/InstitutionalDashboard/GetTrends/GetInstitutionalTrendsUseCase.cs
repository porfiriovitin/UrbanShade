using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetTrends;

public sealed class GetInstitutionalTrendsUseCase : IGetInstitutionalTrendsUseCase
{
    private readonly InstitutionalDashboardQuery _query;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly ILogger<GetInstitutionalTrendsUseCase> _logger;

    public GetInstitutionalTrendsUseCase(InstitutionalDashboardQuery query, IInstitutionalDashboardRepository dashboard, ILogger<GetInstitutionalTrendsUseCase> logger)
    {
        _query = query;
        _dashboard = dashboard;
        _logger = logger;
    }

    public async Task<InstitutionalTrendsResponse> Execute(Guid actorId, Guid regionId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            /// :: Validate filters and access.
            var query = await _query.Prepare(actorId, request, regionId, true, cancellationToken);
            var data = await _dashboard.GetTrends(query, cancellationToken);

            return new(regionId, query.Metric, query.Interval, query.TimeZoneId, data.Select(point => new InstitutionalTrendPointResponse(DateTime.SpecifyKind(point.Date, DateTimeKind.Unspecified), point.Value, point.SampleCount, point.DataQuality, point.AlgorithmVersion, point.Composition, point.DataOrigin)).ToList());
        }
        finally
        {
            _logger.LogInformation("Institutional GetTrends for {OrganizationId} completed in {DurationMs} ms", request.OrganizationId, timer.ElapsedMilliseconds);
        }
    }
}
