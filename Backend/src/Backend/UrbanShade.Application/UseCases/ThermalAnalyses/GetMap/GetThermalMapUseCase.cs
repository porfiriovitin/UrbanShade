using UrbanShade.Application.UseCases.ThermalAnalyses.GetLatest;
using UrbanShade.Communication.Responses.ThermalAnalyses;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.ThermalAnalysis;
using UrbanShade.Application.UseCases.Organizations;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.ThermalAnalyses.GetMap;

public sealed class GetThermalMapUseCase : IGetThermalMapUseCase
{
    private readonly ISensorRepository _sensors;
    private readonly IThermalAnalysisRepository _analyses;
    private readonly ThermalAnalysisSettings _settings;
    private readonly TimeProvider _time;
    private readonly ICityRepository _cities;
    private readonly OrganizationAccess _access;

    public GetThermalMapUseCase(ISensorRepository sensors, IThermalAnalysisRepository analyses, ThermalAnalysisSettings settings, TimeProvider time, ICityRepository cities, OrganizationAccess access)
    {
        _sensors = sensors;
        _analyses = analyses;
        _settings = settings;
        _time = time;
        _cities = cities;
        _access = access;
    }

    public Task<ThermalMapResponse> Execute(CancellationToken cancellationToken = default) => Execute(null, null, null, cancellationToken);

    public async Task<ThermalMapResponse> Execute(Guid? cityId, Guid? organizationId, Guid? actorId, CancellationToken cancellationToken = default)
    {
        if (organizationId.HasValue)
        {
            if (!actorId.HasValue)
                throw new OrganizationException(ErrorKind.Unauthenticated, "An authenticated account is required.");

            await _access.Require(organizationId.Value, actorId.Value, cancellationToken);
        }

        if (cityId.HasValue && await _cities.FindById(cityId.Value, cancellationToken) is null)
            throw new CityException(ErrorKind.NotFound, "City is unavailable.");

        var sensors = await _sensors.GetActiveForThermalMap(cityId, organizationId, cancellationToken);
        if (sensors.Count == 0) return new ThermalMapResponse();

        var analyses = (await _analyses.GetLatestBySensors(sensors.Select(sensor => sensor.Id).ToList(), _settings.AlgorithmVersion, cancellationToken)).ToDictionary(item => item.Reading.SensorId);
        var now = _time.GetUtcNow().UtcDateTime;

        return new ThermalMapResponse
        {
            Sensors = sensors.Select(sensor =>
            {
                var response = GetLatestThermalAnalysisUseCase.ToResponse(sensor.Id, analyses.TryGetValue(sensor.Id, out var item) ? item : null, now, _settings);

                return new ThermalMapSensorResponse
            {
                Id = response.Id,
                SensorId = sensor.Id,
                CityId = sensor.CityId,
                OrganizationId = sensor.OrganizationId,
                SensorName = sensor.Name,
                Latitude = sensor.Latitude,
                Longitude = sensor.Longitude,
                SensorReadingId = response.SensorReadingId,
                Temperature = response.Temperature,
                Humidity = response.Humidity,
                HeatIndex = response.HeatIndex,
                RiskLevel = response.RiskLevel,
                AlgorithmVersion = response.AlgorithmVersion,
                MeasuredAt = response.MeasuredAt,
                AnalyzedAt = response.AnalyzedAt,
                DataStatus = response.DataStatus
            };
            }).OrderBy(sensor => sensor.SensorName).ThenBy(sensor => sensor.SensorId).ToList()
        };
    }
}
