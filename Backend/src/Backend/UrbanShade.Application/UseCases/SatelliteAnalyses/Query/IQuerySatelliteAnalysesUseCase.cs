using System.Net;
using UrbanShade.Application.UseCases.GeoRegions;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.SatelliteAnalyses.Query;

public interface IQuerySatelliteAnalysesUseCase
{
    Task<VegetationResponse> Vegetation(Guid regionId, CancellationToken cancellationToken);
    Task<SurfaceTemperatureResponse> Temperature(Guid regionId, CancellationToken cancellationToken);
    Task<List<SatelliteAnalysisResponse>> History(Guid regionId, DateTime? start, DateTime? end, int page, int pageSize, CancellationToken cancellationToken);
}
