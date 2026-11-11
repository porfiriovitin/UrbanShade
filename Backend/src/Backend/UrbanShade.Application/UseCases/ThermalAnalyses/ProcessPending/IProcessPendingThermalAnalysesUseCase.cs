namespace UrbanShade.Application.UseCases.ThermalAnalyses.ProcessPending;

public interface IProcessPendingThermalAnalysesUseCase
{
    Task Execute(CancellationToken cancellationToken = default);
}
