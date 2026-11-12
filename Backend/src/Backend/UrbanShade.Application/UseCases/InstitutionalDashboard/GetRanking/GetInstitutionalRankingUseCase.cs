using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetRanking;

public sealed class GetInstitutionalRankingUseCase : IGetInstitutionalRankingUseCase
{
    private readonly InstitutionalDashboardQuery _query;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly ILogger<GetInstitutionalRankingUseCase> _logger;

    public GetInstitutionalRankingUseCase(InstitutionalDashboardQuery query, IInstitutionalDashboardRepository dashboard, ILogger<GetInstitutionalRankingUseCase> logger)
    {
        _query = query;
        _dashboard = dashboard;
        _logger = logger;
    }

    public async Task<List<InstitutionalRegionResponse>> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            /// :: Validate filters and access.
            var query = await _query.Prepare(actorId, request, null, false, cancellationToken);
            var data = await _dashboard.GetRegions(query, true, cancellationToken);

            return data.Items.Select(region => _query.ToResponse(region)).ToList();
        }
        finally
        {
            _logger.LogInformation("Institutional GetRanking for {OrganizationId} completed in {DurationMs} ms", request.OrganizationId, timer.ElapsedMilliseconds);
        }
    }
}
