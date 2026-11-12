using UrbanShade.Domain.InstitutionalDashboard;

namespace UrbanShade.Domain.Repositories;

public interface IInstitutionalDashboardRepository
{
    Task<InstitutionalOverviewData> GetOverview(InstitutionalQuery query, CancellationToken cancellationToken);
    Task<InstitutionalSensorData> GetSensorsOverview(InstitutionalQuery query, CancellationToken cancellationToken);
    Task<(List<InstitutionalRegionData> Items, int Total)> GetRegions(InstitutionalQuery query, bool ranking, CancellationToken cancellationToken);
    Task<InstitutionalEnvironmentData> GetEnvironment(InstitutionalQuery query, CancellationToken cancellationToken);
    Task<List<InstitutionalTrendData>> GetTrends(InstitutionalQuery query, CancellationToken cancellationToken);
    Task<List<InstitutionalFeatureData>> GetMap(InstitutionalQuery query, CancellationToken cancellationToken);
    Task<List<InstitutionalReportData>> GetReport(InstitutionalQuery query, CancellationToken cancellationToken);
}
