using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Simulation;

namespace UrbanShade.Application.UseCases.SensorReadings.Generate;

public sealed class GenerateSensorReadingsUseCase : IGenerateSensorReadingsUseCase
{
    private readonly ISensorRepository _sensors;
    private readonly ISensorReadingRepository _readings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SensorReadingGenerator _generator;
    private readonly SensorSimulationSettings _settings;

    public GenerateSensorReadingsUseCase(ISensorRepository sensors, ISensorReadingRepository readings, IUnitOfWork unitOfWork, SensorReadingGenerator generator, SensorSimulationSettings settings)
    {
        _sensors = sensors;
        _readings = readings;
        _unitOfWork = unitOfWork;
        _generator = generator;
        _settings = settings;
    }

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return;

        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);

        /// :: Lock eligible sensors until the readings are committed.
        var sensors = await _sensors.GetActiveForReadingGeneration(cancellationToken);
        if (sensors.Count == 0) return;

        var previous = (await _readings.GetLatestBySensors(sensors.Select(sensor => sensor.Id).ToList(), cancellationToken))
            .ToDictionary(reading => reading.SensorId);

        var readings = sensors.Select(sensor => _generator.Generate(sensor.Id, previous.GetValueOrDefault(sensor.Id))).ToList();
        _readings.AddRange(readings);
        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
    }
}
