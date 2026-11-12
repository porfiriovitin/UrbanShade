using System.Net;
using UrbanShade.Application.UseCases.GeoRegions.Register;
using UrbanShade.Communication.Requests.Geo;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GeoRegions.Update;

public interface IUpdateGeoRegionUseCase
{
    Task<GeoRegionResponse> Execute(Guid id, UpdateGeoRegionRequest request, CancellationToken cancellationToken);

    Task Deactivate(Guid id, CancellationToken cancellationToken);
}
