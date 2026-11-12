using System.Net;
using UrbanShade.Communication.Requests.Geo;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GeoRegions.Register;

public interface IRegisterGeoRegionUseCase
{
    Task<GeoRegionResponse> Execute(RegisterGeoRegionRequest request, CancellationToken cancellationToken);
}
