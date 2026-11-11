using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Simulation;

namespace UrbanShade.Application.UseCases.SensorReadings.Generate;

public sealed class SensorReadingGenerator
{
    private readonly SensorSimulationSettings _settings;
    private readonly Random _random;
    private readonly TimeProvider _time;

    public SensorReadingGenerator(SensorSimulationSettings settings, Random random, TimeProvider time)
    {
        _settings = settings;
        _random = random;
        _time = time;
    }

    public SensorReading Generate(Guid sensorId, SensorReading? previous)
    {
        var temperature = Next(previous?.Temperature, _settings.MinTemperature, _settings.MaxTemperature, _settings.MaxTemperatureVariation);
        var humidity = Next(previous?.Humidity, _settings.MinHumidity, _settings.MaxHumidity, _settings.MaxHumidityVariation);

        /// :: Validate the generated values before they reach persistence.
        if (temperature < _settings.MinTemperature || temperature > _settings.MaxTemperature || humidity < _settings.MinHumidity || humidity > _settings.MaxHumidity)
            throw new InvalidOperationException("Generated reading is outside the configured limits.");

        return new SensorReading(sensorId, temperature, humidity, _time.GetUtcNow().UtcDateTime);
    }

    private decimal Next(decimal? previous, decimal minimum, decimal maximum, decimal variation)
    {
        /// :: Rebase readings when simulation limits change between application starts.
        if (previous.HasValue)
        {
            var baseline = Math.Clamp(previous.Value, minimum, maximum);
            minimum = Math.Max(minimum, baseline - variation);
            maximum = Math.Min(maximum, baseline + variation);
        }

        return Math.Clamp(decimal.Round(minimum + (decimal)_random.NextDouble() * (maximum - minimum), 6), minimum, maximum);
    }
}
