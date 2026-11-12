using UrbanShade.Application.UseCases.GeoRegions.Register;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GeoRegions.Query;

public sealed class QueryGeoRegionsUseCase : IQueryGeoRegionsUseCase
{
    private readonly IGeoRegionRepository _regions;

    public QueryGeoRegionsUseCase(IGeoRegionRepository regions)
    {
        _regions = regions;
    }

    public async Task<List<GeoRegionResponse>> GetAll(Guid? cityId, int page, int pageSize, CancellationToken cancellationToken)
    {
        GeoRegionValidator.Page(page, pageSize);

        return (await _regions.GetAll(cityId, page, pageSize, cancellationToken)).Select(RegisterGeoRegionUseCase.ToResponse).ToList();
    }

    public async Task<GeoRegionResponse> GetById(Guid id, CancellationToken cancellationToken)
    {
        var region = await _regions.FindById(id, cancellationToken);

        return RegisterGeoRegionUseCase.ToResponse(region ?? throw new GeoException(ErrorKind.NotFound, "Region not found."));
    }
}
