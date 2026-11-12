using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetMap;

public sealed class GetInstitutionalMapUseCase : IGetInstitutionalMapUseCase
{
    private readonly InstitutionalDashboardQuery _query;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly InstitutionalDashboardSettings _settings;
    private readonly ILogger<GetInstitutionalMapUseCase> _logger;

    public GetInstitutionalMapUseCase(InstitutionalDashboardQuery query, IInstitutionalDashboardRepository dashboard, InstitutionalDashboardSettings settings, ILogger<GetInstitutionalMapUseCase> logger)
    {
        _query = query;
        _dashboard = dashboard;
        _settings = settings;
        _logger = logger;
    }

    public async Task<InstitutionalMapResponse> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            /// :: Validate filters and access.
            var query = await _query.Prepare(actorId, request, null, false, cancellationToken);
            var data = await _dashboard.GetMap(query, cancellationToken);
            if (data.Count > _settings.MaxMapFeatures || data.Sum(feature => (long)feature.CoordinateCount) > _settings.MaxMapCoordinates)
                throw new InstitutionalDashboardException(ErrorKind.Validation, "Map is too large. Reduce bbox or filters.");

            return new("FeatureCollection", data.Select(feature => new InstitutionalFeatureResponse("Feature", feature.Id, JsonSerializer.Deserialize<JsonElement>(feature.Geometry), JsonSerializer.Deserialize<JsonElement>(feature.Properties))).ToList());
        }
        finally
        {
            _logger.LogInformation("Institutional GetMap for {OrganizationId} completed in {DurationMs} ms", request.OrganizationId, timer.ElapsedMilliseconds);
        }
    }
}
