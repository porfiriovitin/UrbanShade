namespace UrbanShade.Application.UseCases.SensorReadings.Generate;

public interface IGenerateSensorReadingsUseCase
{
    Task Execute(CancellationToken cancellationToken = default);
}
