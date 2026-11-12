namespace UrbanShade.Domain.ThermalAnalysis;

public sealed class HeatIndexCalculator
{
    public decimal Calculate(decimal temperature, decimal humidity)
    {
        if (temperature < -273.15m)
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature cannot be below absolute zero in Celsius.");
        if (humidity < 0 || humidity > 100)
            throw new ArgumentOutOfRangeException(nameof(humidity));

        /// :: Calculate in Fahrenheit; receive and return Celsius.
        var t = (double)temperature * 9 / 5 + 32;
        var rh = (double)humidity;
        var simplified = 0.5 * (t + 61 + (t - 68) * 1.2 + rh * 0.094);
        var heatIndex = (simplified + t) / 2;
        if (heatIndex >= 80)
        {
            heatIndex = -42.379 + 2.04901523 * t + 10.14333127 * rh - 0.22475541 * t * rh - 0.00683783 * t * t - 0.05481717 * rh * rh + 0.00122874 * t * t * rh + 0.00085282 * t * rh * rh - 0.00000199 * t * t * rh * rh;
            if (rh < 13 && t >= 80 && t <= 112)
                heatIndex -= (13 - rh) / 4 * Math.Sqrt((17 - Math.Abs(t - 95)) / 17);
            else if (rh > 85 && t >= 80 && t <= 87)
                heatIndex += (rh - 85) / 10 * (87 - t) / 5;
        }

        return decimal.Round((decimal)((heatIndex - 32) * 5 / 9), 2, MidpointRounding.AwayFromZero);
    }
}
