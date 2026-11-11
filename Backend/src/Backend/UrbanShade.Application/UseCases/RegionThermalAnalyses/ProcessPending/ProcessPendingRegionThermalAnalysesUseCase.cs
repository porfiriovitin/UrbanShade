using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UrbanShade.Application.UseCases.RegionThermalAnalyses.Analyze;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.ProcessPending;

public interface IProcessPendingRegionThermalAnalysesUseCase
{
    Task Execute(CancellationToken cancellationToken = default);
}

public sealed class ProcessPendingRegionThermalAnalysesUseCase : IProcessPendingRegionThermalAnalysesUseCase
{
    private readonly IRegionThermalAnalysisRepository _analyses;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ProcessPendingRegionThermalAnalysesUseCase> _logger;

    public ProcessPendingRegionThermalAnalysesUseCase(IRegionThermalAnalysisRepository analyses, IServiceScopeFactory scopes, ILogger<ProcessPendingRegionThermalAnalysesUseCase> logger)
    {
        _analyses = analyses;
        _scopes = scopes;
        _logger = logger;
    }

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        Guid? after = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var ids = await _analyses.GetActiveRegionIds(after, 100, cancellationToken);
            if (ids.Count == 0) return;
            foreach (var id in ids)
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IAnalyzeRegionThermalUseCase>().Execute(id, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception) { _logger.LogError(exception, "Regional thermal analysis failed for {RegionId}; continuing with other regions", id); }
            }
            after = ids[^1];
        }
    }
}
