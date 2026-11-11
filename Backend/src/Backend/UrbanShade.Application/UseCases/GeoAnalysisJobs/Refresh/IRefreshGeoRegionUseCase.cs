using System.Net;
using System.Text.Json;
using UrbanShade.Communication.Requests.Geo;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GeoAnalysisJobs.Refresh;

public interface IRefreshGeoRegionUseCase
{
    Task<GeoJobResponse> Execute(Guid regionId, RefreshGeoRegionRequest request, CancellationToken cancellationToken);
    Task<GeoJobResponse> GetJob(Guid id, CancellationToken cancellationToken);
}
