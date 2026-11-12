using UrbanShade.Application.UseCases.GeoRegions.Register;
using UrbanShade.Communication.Requests.Geo;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GeoRegions.Update;

public sealed class UpdateGeoRegionUseCase : IUpdateGeoRegionUseCase
{
    private readonly IGeoRegionRepository _regions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GeoAnalysisSettings _settings;
    public UpdateGeoRegionUseCase(IGeoRegionRepository regions, IUnitOfWork unitOfWork, GeoAnalysisSettings settings)
    { _regions = regions; _unitOfWork = unitOfWork; _settings = settings; }
    public async Task<GeoRegionResponse> Execute(Guid id, UpdateGeoRegionRequest request, CancellationToken cancellationToken)
    {
        var region = await _regions.FindById(id, cancellationToken) ?? throw new GeoException(ErrorKind.NotFound, "Region not found.");
        var result = new GeoRegionValidator().Validate(new RegisterGeoRegionRequest { CityId = region.CityId, Name = request.Name ?? region.Name, Description = request.Description ?? region.Description });
        if (!result.IsValid) throw new GeoException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);
        var boundary = request.Boundary.HasValue ? GeoRegionValidator.ReadBoundary(request.Boundary.Value, _settings) : region.Boundary;
        if (await _regions.GetArea(boundary, cancellationToken) > _settings.MaximumRegionAreaSquareMeters) throw new GeoException(ErrorKind.Validation, "Region exceeds the maximum allowed area.");
        region.Update(request.Name ?? region.Name, request.Description ?? region.Description, boundary);
        await _unitOfWork.SaveChanges(cancellationToken);
        return RegisterGeoRegionUseCase.ToResponse(region);
    }
    public async Task Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var region = await _regions.FindById(id, cancellationToken) ?? throw new GeoException(ErrorKind.NotFound, "Region not found.");
        region.Deactivate();
        await _unitOfWork.SaveChanges(cancellationToken);
    }
}
