using UrbanShade.Communication.Responses.Geo;

namespace UrbanShade.Application.UseCases.GreenAreas.Refresh;

public interface IRefreshOpenStreetMapUseCase
{
    Task<GeoJobResponse> Execute(Guid regionId, CancellationToken cancellationToken);
}
