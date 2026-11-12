namespace UrbanShade.Domain.Simulation;

public sealed class SensorSimulationSettings
{
    public bool Enabled { get; set; } = true;
    public double IntervalMinutes { get; set; } = 5;
    public decimal MinTemperature { get; set; } = 18;
    public decimal MaxTemperature { get; set; } = 42;
    public decimal MinHumidity { get; set; } = 20;
    public decimal MaxHumidity { get; set; } = 95;
    public decimal MaxTemperatureVariation { get; set; } = 0.5m;
    public decimal MaxHumidityVariation { get; set; } = 2;

    public void Validate()
    {
        if (!double.IsFinite(IntervalMinutes) || IntervalMinutes < 1d / 60000 || IntervalMinutes > 1440)
            throw new InvalidOperationException("SensorSimulation:IntervalMinutes must represent at least one millisecond and at most 1440 minutes.");
        if (MinTemperature > MaxTemperature || MinTemperature < -1000000 || MaxTemperature > 1000000)
            throw new InvalidOperationException("SensorSimulation temperature limits must be ordered and between -1000000 and 1000000.");
        if (MinHumidity < 0 || MaxHumidity > 100 || MinHumidity > MaxHumidity)
            throw new InvalidOperationException("SensorSimulation humidity limits must be ordered and between 0 and 100.");
        if (MaxTemperatureVariation < 0 || MaxHumidityVariation < 0 || MaxTemperatureVariation > 2000000 || MaxHumidityVariation > 100)
            throw new InvalidOperationException("SensorSimulation variations must be nonnegative and within the supported ranges.");

        /// :: Match the decimal precision used to persist readings.
        decimal[] values = [MinTemperature, MaxTemperature, MinHumidity, MaxHumidity, MaxTemperatureVariation, MaxHumidityVariation];
        if (values.Any(value => decimal.Round(value, 6) != value))
            throw new InvalidOperationException("SensorSimulation values must have at most six decimal places.");
    }
}
