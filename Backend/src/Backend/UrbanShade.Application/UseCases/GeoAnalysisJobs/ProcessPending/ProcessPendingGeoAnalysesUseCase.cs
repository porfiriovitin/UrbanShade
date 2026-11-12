using System.Text.Json;
using Microsoft.Extensions.Logging;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.GeoAnalysisJobs.ProcessPending;

public sealed class ProcessPendingGeoAnalysesUseCase : IProcessPendingGeoAnalysesUseCase
{
    private readonly IGeoAnalysisJobRepository _jobs;
    private readonly ISatelliteAnalysisRepository _analyses;
    private readonly IGreenAreaRepository _greenAreas;
    private readonly IEarthEngineService _earthEngine;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessPendingGeoAnalysesUseCase> _logger;
    public ProcessPendingGeoAnalysesUseCase(IGeoAnalysisJobRepository jobs, ISatelliteAnalysisRepository analyses, IGreenAreaRepository greenAreas,
        IEarthEngineService earthEngine, IUnitOfWork unitOfWork, ILogger<ProcessPendingGeoAnalysesUseCase> logger)
    { _jobs = jobs; _analyses = analyses; _greenAreas = greenAreas; _earthEngine = earthEngine; _unitOfWork = unitOfWork; _logger = logger; }
    public async Task<bool> Execute(CancellationToken cancellationToken)
    {
        var job = await _jobs.Claim(cancellationToken);
        if (job is null) return false;
        var observations = new List<(GeoAnalysisType Type, SatelliteObservation Observation)>();
        var outcomes = new List<string>();
        var failed = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(12));
        try
        {
            var parameters = JsonSerializer.Deserialize<GeoProcessingParameters>(job.Parameters)!;
            foreach (var type in Enum.GetValues<GeoAnalysisType>())
            {
                try
                {
                    if (await _analyses.ExistsForJob(job.Id, type, timeout.Token))
                    {
                        outcomes.Add($"{type}: AVAILABLE");
                        continue;
                    }
                    var observation = await _earthEngine.Analyze(job.Boundary, job.ObservationStartAt, job.ObservationEndAt, type, parameters, timeout.Token);
                    if (observation is null) outcomes.Add($"{type}: UNAVAILABLE");
                    else { observations.Add((type, observation)); outcomes.Add($"{type}: AVAILABLE"); }
                }
                catch (EarthEngineException exception)
                {
                    failed = true;
                    outcomes.Add($"{type}: {exception.Message}");
                    _logger.LogWarning("Satellite source failed for job {JobId}, type {AnalysisType}: {Reason}", job.Id, type, exception.Message);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            failed = true;
            outcomes.Add("Satellite processing could not be completed.");
            _logger.LogWarning(exception, "Satellite processing interrupted for job {JobId}", job.Id);
        }
        /// :: Hold the lease row lock only while saving. No external calls inside this transaction.
        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);
        if (!await _jobs.LockLease(job, cancellationToken)) return true;
        foreach (var (type, observation) in observations)
        {
            if (await _analyses.ExistsForJob(job.Id, type, cancellationToken)) continue;
            var analysis = new SatelliteAnalysis(job, type, observation);
            _analyses.Add(analysis);
            if (type == GeoAnalysisType.Vegetation)
                _greenAreas.AddRange(observation.GreenAreas.Select(area => new GreenArea(job.GeoRegionId, analysis.Id, area.Geometry, area.AreaSquareMeters, observation.ObservedAt)));
        }
        if (failed) job.Fail(string.Join("; ", outcomes).Substring(0, Math.Min(500, string.Join("; ", outcomes).Length)));
        else job.Complete(string.Join("; ", outcomes));
        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
        return true;
    }
}
