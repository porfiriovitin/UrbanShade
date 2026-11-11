using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.GetEnvironment;

public sealed class GetInstitutionalEnvironmentUseCase : IGetInstitutionalEnvironmentUseCase
{
    private readonly InstitutionalDashboardQuery _query;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly ILogger<GetInstitutionalEnvironmentUseCase> _logger;

    public GetInstitutionalEnvironmentUseCase(InstitutionalDashboardQuery query, IInstitutionalDashboardRepository dashboard,
        ILogger<GetInstitutionalEnvironmentUseCase> logger)
    {
        _query = query;
        _dashboard = dashboard;
        _logger = logger;
    }

    public async Task<InstitutionalEnvironmentResponse> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            /// :: Validate filters and authorize the selected institutional scope before reading indicators.
            var query = await _query.Prepare(actorId, request, null, false, cancellationToken);
            var data = await _dashboard.GetEnvironment(query, cancellationToken);
            return new InstitutionalEnvironmentResponse
            {
                TotalAreaSquareMeters = data.TotalAreaSquareMeters, VegetationSampleAreaSquareMeters = data.VegetationSampleAreaSquareMeters,
                SurfaceSampleAreaSquareMeters = data.SurfaceSampleAreaSquareMeters, AverageVegetationCoveragePercent = data.AverageVegetationCoveragePercent,
                AverageNdvi = data.AverageNdvi, AverageSurfaceTemperature = data.AverageSurfaceTemperature,
                MaximumSurfaceTemperature = data.MaximumSurfaceTemperature, AverageSurfaceTemperatureAnomaly = data.AverageSurfaceTemperatureAnomaly,
                VegetationSampleCount = data.VegetationSampleCount, SurfaceSampleCount = data.SurfaceSampleCount, AnomalySampleCount = data.AnomalySampleCount,
                LowVegetationRegions = data.LowVegetationRegions, HeatIslandCandidates = data.HeatIslandCandidates,
                FirstObservationAt = data.FirstObservationAt, LastObservationAt = data.LastObservationAt
            };

        }
        finally
        {
            _logger.LogInformation("Institutional GetEnvironment for {OrganizationId} completed in {DurationMs} ms", request.OrganizationId, timer.ElapsedMilliseconds);
        }
    }
}
