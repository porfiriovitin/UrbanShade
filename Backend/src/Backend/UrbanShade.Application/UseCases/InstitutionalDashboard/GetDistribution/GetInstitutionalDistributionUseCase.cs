using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetDistribution;

public sealed class GetInstitutionalDistributionUseCase : IGetInstitutionalDistributionUseCase
{
    private readonly InstitutionalDashboardQuery _query;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly ILogger<GetInstitutionalDistributionUseCase> _logger;

    public GetInstitutionalDistributionUseCase(InstitutionalDashboardQuery query, IInstitutionalDashboardRepository dashboard, ILogger<GetInstitutionalDistributionUseCase> logger)
    {
        _query = query;
        _dashboard = dashboard;
        _logger = logger;
    }

    public async Task<InstitutionalDistributionResponse> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            /// :: Validate filters and access.
            var query = await _query.Prepare(actorId, request, null, false, cancellationToken);
            var data = await _dashboard.GetOverview(query, cancellationToken);

            return new(data.TotalRegions, data.Low, data.Moderate, data.High, data.Critical, data.InsufficientData);
        }
        finally
        {
            _logger.LogInformation("Institutional GetDistribution for {OrganizationId} completed in {DurationMs} ms", request.OrganizationId, timer.ElapsedMilliseconds);
        }
    }
}
