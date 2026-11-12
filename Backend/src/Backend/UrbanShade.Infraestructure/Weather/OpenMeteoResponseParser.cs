using System.Text.Json;
using UrbanShade.Domain.Weather;

namespace UrbanShade.Infraestructure.Weather;

public static class OpenMeteoResponseParser
{
    public static WeatherObservation Parse(JsonElement root, DateTime fetchedAt)
    {
        try
        {
            if (fetchedAt.Kind != DateTimeKind.Utc || root.GetProperty("utc_offset_seconds").GetInt32() != 0)
                throw new WeatherProviderException("Open-Meteo returned an invalid timezone.");
            var current = root.GetProperty("current");
            var hourly = root.GetProperty("hourly");
            Units(root.GetProperty("current_units"), false);
            Units(root.GetProperty("hourly_units"), true);
            var observedAt = Time(current.GetProperty("time"));
            if (observedAt > fetchedAt.AddHours(1)) throw new WeatherProviderException("Open-Meteo returned a future current reference time.");
            var conditions = new WeatherConditions(Value(current, "temperature_2m"), Value(current, "relative_humidity_2m", 0, 100),
                Value(current, "apparent_temperature"), Value(current, "wind_speed_10m", 0), Value(current, "precipitation", 0),
                Value(current, "cloud_cover", 0, 100), Code(current));
            if (conditions.Temperature is null && conditions.Humidity is null && conditions.ApparentTemperature is null)
                throw new WeatherProviderException("Open-Meteo returned no usable current conditions.");
            var times = hourly.GetProperty("time");
            if (times.ValueKind != JsonValueKind.Array || times.GetArrayLength() is < 1 or > 384)
                throw new WeatherProviderException("Open-Meteo returned an invalid forecast length.");
            foreach (var property in hourly.EnumerateObject().Where(property => property.Name != "time"))
                if (property.Value.ValueKind != JsonValueKind.Array || property.Value.GetArrayLength() != times.GetArrayLength())
                    throw new WeatherProviderException("Open-Meteo returned misaligned forecast arrays.");
            var predictions = new List<WeatherPrediction>();
            DateTime? previous = null;
            for (var index = 0; index < times.GetArrayLength(); index++)
            {
                var at = Time(times[index]);
                if (previous.HasValue && at != previous.Value.AddHours(1)) throw new WeatherProviderException("Open-Meteo returned unordered or non-hourly forecasts.");
                previous = at;
                var prediction = new WeatherPrediction(at, Value(hourly, "temperature_2m", index: index), Value(hourly, "relative_humidity_2m", 0, 100, index),
                    Value(hourly, "apparent_temperature", index: index), Value(hourly, "precipitation_probability", 0, 100, index),
                    Value(hourly, "wind_speed_10m", 0, index: index), Value(hourly, "cloud_cover", 0, 100, index),
                    Value(hourly, "uv_index", 0, index: index), Value(hourly, "shortwave_radiation", 0, index: index));
                if (at > fetchedAt && (prediction.Temperature.HasValue || prediction.Humidity.HasValue || prediction.ApparentTemperature.HasValue))
                    predictions.Add(prediction);
            }
            if (predictions.Count == 0) throw new WeatherProviderException("Open-Meteo returned no usable future forecasts.");
            return new(observedAt, fetchedAt, conditions, predictions) { Source = "OPEN_METEO" };
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentOutOfRangeException)
        { throw new WeatherProviderException("Open-Meteo returned an invalid weather response."); }
    }

    private static DateTime Time(JsonElement value) => DateTimeOffset.FromUnixTimeSeconds(value.GetInt64()).UtcDateTime;
    private static int? Code(JsonElement current)
    {
        var code = Value(current, "weather_code", 0, 99);
        if (code.HasValue && decimal.Truncate(code.Value) != code.Value) throw new WeatherProviderException("Open-Meteo returned an invalid weather code.");
        return (int?)code;
    }
    private static decimal? Value(JsonElement parent, string name, decimal? minimum = null, decimal? maximum = null, int? index = null)
    {
        if (!parent.TryGetProperty(name, out var value)) return null;
        if (index.HasValue) value = value[index.Value];
        if (value.ValueKind == JsonValueKind.Null) return null;
        var number = value.GetDecimal();
        if ((minimum.HasValue && number < minimum) || (maximum.HasValue && number > maximum))
            throw new WeatherProviderException($"Open-Meteo returned an invalid {name} value.");
        return number;
    }
    private static void Units(JsonElement units, bool hourly)
    {
        var expected = new Dictionary<string, string>
        {
            ["time"] = "unixtime", ["temperature_2m"] = "°C", ["relative_humidity_2m"] = "%", ["apparent_temperature"] = "°C",
            ["wind_speed_10m"] = "km/h", ["cloud_cover"] = "%"
        };
        if (hourly) { expected["precipitation_probability"] = "%"; expected["uv_index"] = ""; expected["shortwave_radiation"] = "W/m²"; }
        else { expected["precipitation"] = "mm"; expected["weather_code"] = "wmo code"; }
        foreach (var unit in expected)
            if (units.TryGetProperty(unit.Key, out var value) && value.GetString() != unit.Value)
                throw new WeatherProviderException($"Open-Meteo returned an unexpected unit for {unit.Key}.");
    }
}
