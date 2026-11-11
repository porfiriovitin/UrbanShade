using System.Text.Json;
using Microsoft.Extensions.Logging;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.GreenAreas.Import;

public sealed class ImportOpenStreetMapUseCase : IImportOpenStreetMapUseCase
{
    private readonly IGeoAnalysisJobRepository _jobs;
    private readonly IGreenAreaRepository _areas;
    private readonly IOpenStreetMapService _openStreetMap;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ImportOpenStreetMapUseCase> _logger;

    public ImportOpenStreetMapUseCase(IGeoAnalysisJobRepository jobs, IGreenAreaRepository areas, IOpenStreetMapService openStreetMap,
        IUnitOfWork unitOfWork, ILogger<ImportOpenStreetMapUseCase> logger)
    {
        _jobs = jobs;
        _areas = areas;
        _openStreetMap = openStreetMap;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Execute(CancellationToken cancellationToken)
    {
        var job = await _jobs.Claim(cancellationToken, GeoJobType.OpenStreetMap);
        if (job is null) return false;
        OpenStreetMapSnapshot? snapshot = null;
        string? error = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            var parameters = JsonSerializer.Deserialize<OpenStreetMapParameters>(job.Parameters)!;
            snapshot = await _openStreetMap.GetGreenAreas(job.Boundary, parameters, timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OpenStreetMapException exception) { error = exception.Message; }
        catch (Exception exception)
        {
            error = "OpenStreetMap import could not be completed. Previous data was preserved.";
            _logger.LogWarning("OpenStreetMap job {JobId} failed with {ExceptionType}", job.Id, exception.GetType().Name);
        }

        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);
        if (!await _jobs.LockLease(job, cancellationToken)) return true;
        if (error is not null) job.Fail(error[..Math.Min(500, error.Length)]);
        else
        {
            var imported = await _areas.ImportOpenStreetMap(job, snapshot!, cancellationToken);
            job.Complete(imported ? $"OPENSTREETMAP: imported snapshot with {snapshot!.Areas.Count} candidate areas." : "OPENSTREETMAP: skipped superseded import or inactive region.");
        }
        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
        return true;
    }
}
