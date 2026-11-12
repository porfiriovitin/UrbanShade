using System.Net;
using UrbanShade.Application.UseCases.GeoRegions.Register;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GeoRegions.Query;

public interface IQueryGeoRegionsUseCase
{
    Task<List<GeoRegionResponse>> GetAll(Guid? cityId, int page, int pageSize, CancellationToken cancellationToken);

    Task<GeoRegionResponse> GetById(Guid id, CancellationToken cancellationToken);
}
