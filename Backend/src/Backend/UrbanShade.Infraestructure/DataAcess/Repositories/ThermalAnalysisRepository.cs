using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class ThermalAnalysisRepository : IThermalAnalysisRepository
{
    private readonly UrbanShadeDbContext _context;

    public ThermalAnalysisRepository(UrbanShadeDbContext context)
    {
        _context = context;
    }

    public Task<List<SensorReading>> GetPending(string algorithmVersion, int take, List<Guid> excludedReadingIds, CancellationToken cancellationToken) => _context.SensorReadings.AsNoTracking().Where(reading => !excludedReadingIds.Contains(reading.Id) && _context.Sensors.Any(sensor => sensor.Id == reading.SensorId && sensor.Active && sensor.Status == SensorStatus.ACTIVE) && !_context.ThermalAnalyses.Any(analysis => analysis.SensorReadingId == reading.Id && analysis.AlgorithmVersion == algorithmVersion)).OrderBy(reading => reading.MeasuredAt).ThenBy(reading => reading.Id).Take(take).ToListAsync(cancellationToken);

    public async Task<bool> TryAddForActiveSensor(ThermalAnalysis analysis, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var sensorId = await _context.SensorReadings.Where(reading => reading.Id == analysis.SensorReadingId).Select(reading => (Guid?)reading.SensorId).SingleOrDefaultAsync(cancellationToken);
        if (!sensorId.HasValue) return false;

        /// :: Lock sensor status before checking eligibility.
        var sensors = await _context.Sensors.FromSqlInterpolated($"""
            SELECT * FROM "Sensors"
            WHERE "Id" = {sensorId.Value} AND "Status" = 'ACTIVE' AND "Active" = TRUE AND "DeletedAt" IS NULL
            FOR UPDATE
            """).AsNoTracking().ToListAsync(cancellationToken);
        if (sensors.Count == 0) return false;

        /// :: Prevent duplicate analyses for this version.
        var inserted = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ThermalAnalyses" ("Id", "SensorReadingId", "HeatIndex", "RiskLevel", "AlgorithmVersion", "AnalyzedAt", "Active", "CreatedAt")
            VALUES ({analysis.Id}, {analysis.SensorReadingId}, {analysis.HeatIndex}, {analysis.RiskLevel.ToString()}, {analysis.AlgorithmVersion}, {analysis.AnalyzedAt}, {analysis.Active}, {analysis.CreatedAt})
            ON CONFLICT ("SensorReadingId", "AlgorithmVersion") DO NOTHING
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return inserted == 1;
    }

    public async Task<(SensorReading Reading, ThermalAnalysis Analysis)?> GetLatest(Guid sensorId, string algorithmVersion, CancellationToken cancellationToken)
    {
        var item = await Query(algorithmVersion).Where(analysis => analysis.Reading.SensorId == sensorId).OrderByDescending(analysis => analysis.Reading.MeasuredAt).ThenByDescending(analysis => analysis.Reading.Id).FirstOrDefaultAsync(cancellationToken);

        return item is null ? null : (item.Reading, item.Analysis);
    }

    public async Task<List<(SensorReading Reading, ThermalAnalysis Analysis)>> GetLatestBySensors(List<Guid> sensorIds, string algorithmVersion, CancellationToken cancellationToken)
    {
        var items = await Query(algorithmVersion).Where(analysis => sensorIds.Contains(analysis.Reading.SensorId)).GroupBy(analysis => analysis.Reading.SensorId).Select(group => group.OrderByDescending(analysis => analysis.Reading.MeasuredAt).ThenByDescending(analysis => analysis.Reading.Id).First()).ToListAsync(cancellationToken);

        return items.Select(item => (item.Reading, item.Analysis)).ToList();
    }

    public async Task<(List<(SensorReading Reading, ThermalAnalysis Analysis)> Items, int TotalItems)> GetHistory(Guid sensorId, string algorithmVersion, DateTime? startDate, DateTime? endDate, ThermalRiskLevel? riskLevel, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Query(algorithmVersion).Where(analysis => analysis.Reading.SensorId == sensorId);
        if (startDate.HasValue)
            query = query.Where(analysis => analysis.Reading.MeasuredAt >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(analysis => analysis.Reading.MeasuredAt <= endDate.Value);
        if (riskLevel.HasValue)
            query = query.Where(analysis => analysis.Analysis.RiskLevel == riskLevel.Value);

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(analysis => analysis.Reading.MeasuredAt).ThenByDescending(analysis => analysis.Reading.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return (items.Select(item => (item.Reading, item.Analysis)).ToList(), count);
    }

    private IQueryable<AnalysisReading> Query(string algorithmVersion) =>
        from analysis in _context.ThermalAnalyses.AsNoTracking()join reading in _context.SensorReadings.AsNoTracking()on analysis.SensorReadingId equals reading.Id
        where analysis.Active && analysis.AlgorithmVersion == algorithmVersion
        select new AnalysisReading
    {
        Reading = reading,
        Analysis = analysis
    };

    private sealed class AnalysisReading
    {
        public SensorReading Reading { get; init; } = null!;
        public ThermalAnalysis Analysis { get; init; } = null!;
    }
}
