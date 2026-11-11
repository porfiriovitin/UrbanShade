using System.Net;
using UrbanShade.Application.UseCases.ThermalAnalyses.GetLatest;
using UrbanShade.Communication.Requests.ThermalAnalyses;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.ThermalAnalyses;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.ThermalAnalysis;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.ThermalAnalyses.GetHistory;

public sealed class GetThermalAnalysesUseCase : IGetThermalAnalysesUseCase
{
    private readonly ISensorRepository _sensors;
    private readonly IThermalAnalysisRepository _analyses;
    private readonly ThermalAnalysisSettings _settings;
    private readonly TimeProvider _time;

    public GetThermalAnalysesUseCase(ISensorRepository sensors, IThermalAnalysisRepository analyses, ThermalAnalysisSettings settings, TimeProvider time)
    {
        _sensors = sensors;
        _analyses = analyses;
        _settings = settings;
        _time = time;
    }

    public async Task<PagedResponse<ThermalAnalysisResponse>> Execute(Guid sensorId, GetThermalAnalysesRequest request, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        var result = new GetThermalAnalysesValidator().Validate(request);
        if (!result.IsValid)
            throw new SensorException(HttpStatusCode.BadRequest, [.. result.Errors.Select(error => error.ErrorMessage)]);

        var sensor = await _sensors.FindForReadingHistory(sensorId, cancellationToken);
        if (sensor is null || (!isAdministrator && (!sensor.Active || sensor.DeletedAt.HasValue || sensor.Status != SensorStatus.ACTIVE)))
            throw new SensorException(HttpStatusCode.NotFound, "Sensor is unavailable.");

        ThermalRiskLevel? risk = request.RiskLevel is null ? null : Enum.Parse<ThermalRiskLevel>(request.RiskLevel, true);
        var history = await _analyses.GetHistory(sensorId, request.AlgorithmVersion ?? _settings.AlgorithmVersion,
            request.StartDate?.UtcDateTime, request.EndDate?.UtcDateTime, risk, request.Page, request.PageSize, cancellationToken);
        var now = _time.GetUtcNow().UtcDateTime;

        return new PagedResponse<ThermalAnalysisResponse>
        {
            Items = history.Items.Select(item => GetLatestThermalAnalysisUseCase.ToResponse(sensorId, item, now, _settings)).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = history.TotalItems
        };
    }
}
