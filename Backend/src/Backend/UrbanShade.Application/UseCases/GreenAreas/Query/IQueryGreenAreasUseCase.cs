using System.Globalization;
using System.Net;
using System.Text.Json;
using NetTopologySuite.Geometries;
using UrbanShade.Application.UseCases.GeoRegions;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GreenAreas.Query;

public interface IQueryGreenAreasUseCase
{
    Task<JsonElement> Execute(string bbox, int page, int pageSize, CancellationToken cancellationToken);
}
