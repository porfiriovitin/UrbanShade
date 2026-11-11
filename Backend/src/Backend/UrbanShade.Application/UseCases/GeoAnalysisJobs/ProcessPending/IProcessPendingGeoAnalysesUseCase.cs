using System.Text.Json;
using Microsoft.Extensions.Logging;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Application.UseCases.GeoAnalysisJobs.ProcessPending;

public interface IProcessPendingGeoAnalysesUseCase { Task<bool> Execute(CancellationToken cancellationToken); }
