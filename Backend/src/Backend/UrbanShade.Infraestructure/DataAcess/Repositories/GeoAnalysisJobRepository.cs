using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class GeoAnalysisJobRepository : IGeoAnalysisJobRepository
{
    private readonly UrbanShadeDbContext _context;
    public GeoAnalysisJobRepository(UrbanShadeDbContext context) { _context = context; }
    public async Task<GeoAnalysisJob> Enqueue(GeoAnalysisJob job, CancellationToken cancellationToken)
    {
        /// :: PostgreSQL arbitrates concurrent refresh requests using the same unique processing identity.
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "GeoAnalysisJobs" ("Id", "Active", "CreatedAt", "GeoRegionId", "Boundary", "BoundaryVersion", "ObservationStartAt", "ObservationEndAt", "AlgorithmVersion", "Parameters", "JobType", "Status", "Attempts")
            VALUES ({job.Id}, true, {job.CreatedAt}, {job.GeoRegionId}, ST_GeomFromText({job.Boundary.AsText()},4326), {job.BoundaryVersion}, {job.ObservationStartAt}, {job.ObservationEndAt}, {job.AlgorithmVersion}, {job.Parameters}, {job.JobType.ToString()}, 'Pending', 0)
            ON CONFLICT ("GeoRegionId", "BoundaryVersion", "ObservationStartAt", "ObservationEndAt", "AlgorithmVersion", "Parameters", "JobType") DO NOTHING
            """, cancellationToken);
        var existing = await _context.GeoAnalysisJobs.SingleAsync(item => item.GeoRegionId == job.GeoRegionId && item.BoundaryVersion == job.BoundaryVersion &&
            item.ObservationStartAt == job.ObservationStartAt && item.ObservationEndAt == job.ObservationEndAt && item.AlgorithmVersion == job.AlgorithmVersion && item.Parameters == job.Parameters && item.JobType == job.JobType, cancellationToken);
        if (existing.Status == GeoJobStatus.Failed)
        {
            existing.Retry();
            await _context.SaveChangesAsync(cancellationToken);
        }
        return existing;
    }
    public Task<GeoAnalysisJob?> FindById(Guid id, CancellationToken cancellationToken) => _context.GeoAnalysisJobs.AsNoTracking().SingleOrDefaultAsync(job => job.Id == id, cancellationToken);
    public async Task<GeoAnalysisJob?> Claim(CancellationToken cancellationToken, GeoJobType jobType = GeoJobType.Satellite)
    {
        var leaseId = Guid.NewGuid();
        var jobs = await _context.GeoAnalysisJobs.FromSqlInterpolated($"""
            UPDATE "GeoAnalysisJobs" SET "Status" = 'Processing', "StartedAt" = now(), "FinishedAt" = NULL,
                "LeaseId" = {leaseId}, "LeaseExpiresAt" = now() + interval '15 minutes', "Attempts" = "Attempts" + 1
            WHERE "Id" = (SELECT "Id" FROM "GeoAnalysisJobs" WHERE "JobType" = {jobType.ToString()} AND ("Status" = 'Pending' OR ("Status" = 'Processing' AND "LeaseExpiresAt" < now()))
                ORDER BY "CreatedAt" FOR UPDATE SKIP LOCKED LIMIT 1)
            RETURNING *
            """).ToListAsync(cancellationToken);
        return jobs.SingleOrDefault();
    }
    public async Task<bool> LockLease(GeoAnalysisJob job, CancellationToken cancellationToken)
    {
        var ids = await _context.Database.SqlQuery<Guid>($"""
            SELECT "Id" AS "Value" FROM "GeoAnalysisJobs"
            WHERE "Id" = {job.Id} AND "LeaseId" = {job.LeaseId} AND "Status" = 'Processing' AND "LeaseExpiresAt" > now()
            FOR UPDATE
            """).ToListAsync(cancellationToken);
        return ids.Count == 1;
    }
    public Task<List<GeoRegion>> GetDueRegions(DateTime cutoff, int limit, CancellationToken cancellationToken, GeoJobType jobType = GeoJobType.Satellite) =>
        _context.GeoRegions.AsNoTracking().Where(region => region.Active && !_context.GeoAnalysisJobs.Any(job => job.GeoRegionId == region.Id && job.JobType == jobType && job.BoundaryVersion == region.BoundaryVersion && job.CreatedAt > cutoff))
        .OrderBy(region => region.Id).Take(limit).ToListAsync(cancellationToken);
}
