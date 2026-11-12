using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Communication.Responses.InstitutionalDashboard;
using UrbanShade.Domain.InstitutionalDashboard;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard.ExportReport;

public sealed class ExportInstitutionalReportUseCase : IExportInstitutionalReportUseCase
{
    private readonly InstitutionalDashboardQuery _query;
    private readonly IInstitutionalDashboardRepository _dashboard;
    private readonly InstitutionalDashboardSettings _settings;
    private readonly ILogger<ExportInstitutionalReportUseCase> _logger;

    public ExportInstitutionalReportUseCase(InstitutionalDashboardQuery query, IInstitutionalDashboardRepository dashboard, InstitutionalDashboardSettings settings, ILogger<ExportInstitutionalReportUseCase> logger)
    {
        _query = query;
        _dashboard = dashboard;
        _settings = settings;
        _logger = logger;
    }

    public async Task<InstitutionalExportResponse> Execute(Guid actorId, InstitutionalDashboardRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            /// :: Validate filters and access.
            var query = await _query.Prepare(actorId, request, null, true, cancellationToken);
            var rows = await _dashboard.GetReport(query, cancellationToken);
            if (rows.Count > _settings.MaxExportRows)
                throw new InstitutionalDashboardException(ErrorKind.Validation, "Export is too large. Reduce the period or select a region.");

            var csv = BuildCsv(rows, cancellationToken);
            _logger.LogInformation("Exported {RowCount} institutional analyses for {OrganizationId}", rows.Count, query.OrganizationId);

            return new(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", $"institutional-{query.OrganizationId:N}.csv");
        }
        finally
        {
            _logger.LogInformation("Institutional ExportReport for {OrganizationId} completed in {DurationMs} ms", request.OrganizationId, timer.ElapsedMilliseconds);
        }
    }

    internal static string CsvCell(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        var first = text.TrimStart().FirstOrDefault();
        if (value is string && (first is '=' or '+' or '-' or '@' || text.StartsWith('\t') || text.StartsWith('\r') || text.StartsWith('\n')))
            text = "'" + text;

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static StringBuilder BuildCsv(IReadOnlyList<InstitutionalReportData> rows, CancellationToken cancellationToken)
    {
        var csv = new StringBuilder("regionId,regionName,analyzedAt,exposureIndex,exposureLevel,heatIndex,surfaceTemperature,vegetationCoveragePercent,algorithmVersion,boundaryVersion,dataQuality,dataOrigin,sourceMetadata\r\n");

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            csv.Append(string.Join(",", new object?[] { row.RegionId, row.RegionName, row.AnalyzedAt.ToString("O"), row.ExposureIndex, row.ExposureLevel, row.HeatIndex, row.SurfaceTemperature, row.VegetationCoveragePercent, row.AlgorithmVersion, row.BoundaryVersion, row.DataQuality, row.DataOrigin, UrbanShade.Application.Mapping.ProvenanceMapper.PublicMetadata(row.SourceMetadata).GetRawText() }.Select(CsvCell))).Append("\r\n");
        }

        return csv;
    }
}
