namespace UrbanShade.Application.UseCases.GreenAreas.Import;

public interface IImportOpenStreetMapUseCase
{
    Task<bool> Execute(CancellationToken cancellationToken);
}
