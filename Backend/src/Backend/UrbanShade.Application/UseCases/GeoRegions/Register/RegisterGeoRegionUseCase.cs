using System.Net;
using UrbanShade.Communication.Requests.Geo;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GeoRegions.Register;

public sealed class RegisterGeoRegionUseCase : IRegisterGeoRegionUseCase
{
    private readonly IGeoRegionRepository _regions;
    private readonly ICityRepository _cities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GeoAnalysisSettings _settings;
    public RegisterGeoRegionUseCase(IGeoRegionRepository regions, ICityRepository cities, IUnitOfWork unitOfWork, GeoAnalysisSettings settings)
    { _regions = regions; _cities = cities; _unitOfWork = unitOfWork; _settings = settings; }
    public async Task<GeoRegionResponse> Execute(RegisterGeoRegionRequest request, CancellationToken cancellationToken)
    {
        var result = new GeoRegionValidator().Validate(request);
        if (!result.IsValid) throw new GeoException(HttpStatusCode.BadRequest, [.. result.Errors.Select(error => error.ErrorMessage)]);
        if (await _cities.FindById(request.CityId, cancellationToken) is null) throw new GeoException(HttpStatusCode.NotFound, "City not found.");
        var boundary = GeoRegionValidator.ReadBoundary(request.Boundary, _settings);
        if (await _regions.GetArea(boundary, cancellationToken) > _settings.MaximumRegionAreaSquareMeters)
            throw new GeoException(HttpStatusCode.BadRequest, "Region exceeds the maximum allowed area.");
        var region = new GeoRegion(request.CityId, request.Name, request.Description, boundary);
        _regions.Add(region);
        await _unitOfWork.SaveChanges(cancellationToken);
        return ToResponse(region);
    }
    public static GeoRegionResponse ToResponse(GeoRegion region) => new(region.Id, region.CityId, region.Name, region.Description, GeoJsonGeometry.Write(region.Boundary), region.Active, region.BoundaryVersion, region.CreatedAt, region.UpdatedAt);
}
