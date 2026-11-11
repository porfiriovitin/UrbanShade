using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.CompareRegions;

public sealed class CompareInstitutionalRegionsUseCase : ICompareInstitutionalRegionsUseCase
{
    private readonly InstitutionalDashboardQuery _query;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly ILogger<CompareInstitutionalRegionsUseCase> _logger;

    public CompareInstitutionalRegionsUseCase(InstitutionalDashboardQuery query, IInstitutionalDashboardRepository dashboard,
        ILogger<CompareInstitutionalRegionsUseCase> logger)
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
            /// :: Validate filters and authorize the selected institutional scope before reading indicators.
            var query = await _query.Prepare(actorId, request, null, false, cancellationToken);
            if (query.RegionIds.Length < 2)
                throw new InstitutionalDashboardException(HttpStatusCode.BadRequest, "Provide between two and ten regionIds for comparison.");
            var data = await _dashboard.GetRegions(query with { Page = 1, PageSize = 10 }, false, cancellationToken);
            return data.Items.Select(region => _query.ToResponse(region)).ToList();

        }
        finally
        {
            _logger.LogInformation("Institutional CompareRegions for {OrganizationId} completed in {DurationMs} ms", request.OrganizationId, timer.ElapsedMilliseconds);
        }
    }
}
