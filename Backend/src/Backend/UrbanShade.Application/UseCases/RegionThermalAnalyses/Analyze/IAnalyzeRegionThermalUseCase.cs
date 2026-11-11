using UrbanShade.Communication.Responses.RegionThermalAnalyses;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.Analyze;

public interface IAnalyzeRegionThermalUseCase
{
    Task<RegionThermalAnalysisResponse> Execute(Guid id, CancellationToken cancellationToken = default);
}
