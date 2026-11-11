using NetTopologySuite.Geometries;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.RegionThermalAnalysis;

namespace UrbanShade.Domain.Repositories;

public interface IRegionThermalAnalysisRepository
{
    Task<IRegionAnalysisTransaction?> Acquire(Guid regionId, CancellationToken cancellationToken);
    Task<RegionAnalysisData?> GetData(Guid regionId, DateTime now, CancellationToken cancellationToken);
    void Add(Entities.RegionThermalAnalysis analysis);
    Task<Entities.RegionThermalAnalysis?> GetLatest(Guid regionId, int? boundaryVersion, CancellationToken cancellationToken);
    Task<(List<Entities.RegionThermalAnalysis> Items, int Total)> GetHistory(Guid regionId, DateTime? start, DateTime? end, ExposureLevel? level, int page, int pageSize, CancellationToken cancellationToken);
    Task<List<(GeoRegion Region, Entities.RegionThermalAnalysis Analysis)>> GetLatestRegions(List<Guid>? ids, Geometry? bounds, decimal? minIndex, ExposureLevel? level, bool includePartial, bool ranking, int take, CancellationToken cancellationToken);
    Task<List<Guid>> GetActiveRegionIds(Guid? after, int take, CancellationToken cancellationToken);
}

public interface IRegionAnalysisTransaction : IAsyncDisposable
{
    Task Commit(CancellationToken cancellationToken);
}
