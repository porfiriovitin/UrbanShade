using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetRegions;

public sealed class GetInstitutionalRegionsUseCase : IGetInstitutionalRegionsUseCase
{
    private readonly InstitutionalDashboardQuery _query;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly ILogger<GetInstitutionalRegionsUseCase> _logger;

    public GetInstitutionalRegionsUseCase(InstitutionalDashboardQuery query, IInstitutionalDashboardRepository dashboard,
        ILogger<GetInstitutionalRegionsUseCase> logger)
    {
        _query = query;
        _dashboard = dashboard;
        _logger = logger;
    }

    public async Task<PagedResponse<InstitutionalRegionResponse>> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            /// :: Validate filters and authorize the selected institutional scope before reading indicators.
            var query = await _query.Prepare(actorId, request, null, false, cancellationToken);
            var data = await _dashboard.GetRegions(query, false, cancellationToken);
            return new PagedResponse<InstitutionalRegionResponse>
            {
                Items = data.Items.Select(region => _query.ToResponse(region)).ToList(),
                Page = query.Page, PageSize = query.PageSize, TotalItems = data.Total
            };

        }
        finally
        {
            _logger.LogInformation("Institutional GetRegions for {OrganizationId} completed in {DurationMs} ms", request.OrganizationId, timer.ElapsedMilliseconds);
        }
    }
}
