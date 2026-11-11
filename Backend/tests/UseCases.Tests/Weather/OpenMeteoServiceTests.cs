using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UrbanShade.Domain.Weather;
using UrbanShade.Infraestructure.Weather;

namespace UseCases.Tests.Weather;

public sealed class OpenMeteoServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Request_UsesInvariantCoordinatesExplicitUnitsAndUtc_ParsesNullableFields()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var handler = new Handler((_, _) => Task.FromResult(JsonResponse()));
            var observation = await Client(handler).GetWeather(-23.587, -46.656, default);
            Assert.Contains("latitude=-23.587&longitude=-46.656", handler.Url);
            Assert.Contains("forecast_hours=73", handler.Url);
            Assert.Contains("timezone=UTC&timeformat=unixtime&temperature_unit=celsius&wind_speed_unit=kmh&precipitation_unit=mm", handler.Url);
            Assert.Equal(30.5m, observation.Current.Temperature);
            Assert.Null(observation.Current.Precipitation);
            Assert.Equal(DateTimeKind.Utc, observation.ObservedAt.Kind);
            Assert.Equal(Now, observation.FetchedAt);
            var prediction = Assert.Single(observation.Hourly);
            Assert.Equal(8m, prediction.UvIndex);
            Assert.Equal(780m, prediction.SolarRadiation);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData(91, 0, 3)]
    [InlineData(0, -181, 3)]
    [InlineData(0, 0, 17)]
    [InlineData(double.NaN, 0, 3)]
    public void InvalidCoordinatesAndPeriod_AreRejected(double latitude, double longitude, int days) =>
        Assert.Throws<OpenMeteoException>(() => OpenMeteoService.BuildQuery(latitude, longitude, days));

    [Fact]
    public void MaximumForecastPeriod_RespectsProviderHourLimit() =>
        Assert.Contains("forecast_hours=384", OpenMeteoService.BuildQuery(0, 0, 16));

    [Theory]
    [InlineData("units")]
    [InlineData("array")]
    [InlineData("humidity")]
    [InlineData("time")]
    [InlineData("empty")]
    [InlineData("timezone")]
    public void InvalidProviderData_IsRejected(string failure)
    {
        var json = JsonNode.Parse(Snapshot())!;
        switch (failure)
        {
            case "units": json["current_units"]!["temperature_2m"] = "°F"; break;
            case "array": json["hourly"]!["uv_index"] = new JsonArray(1); break;
            case "humidity": json["current"]!["relative_humidity_2m"] = 101; break;
            case "time": json["hourly"]!["time"]![1] = json["hourly"]!["time"]![0]!.DeepClone(); break;
            case "empty": json["current"]!["temperature_2m"] = null; json["current"]!["relative_humidity_2m"] = null; json["current"]!["apparent_temperature"] = null; break;
            case "timezone": json["utc_offset_seconds"] = -10800; break;
        }
        using var document = JsonDocument.Parse(json.ToJsonString());
        Assert.Throws<OpenMeteoException>(() => OpenMeteoResponseParser.Parse(document.RootElement, Now));
    }

    [Fact]
    public void MissingOptionalVariable_IsPreservedAsNull()
    {
        var json = JsonNode.Parse(Snapshot())!;
        json["hourly"]!.AsObject().Remove("uv_index");
        using var document = JsonDocument.Parse(json.ToJsonString());
        Assert.Null(Assert.Single(OpenMeteoResponseParser.Parse(document.RootElement, Now).Hourly).UvIndex);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    public async Task TransientStatus_RetriesWithExponentialBackoff_ThenSucceeds(int status)
    {
        var time = new FastTime();
        var handler = new Handler((attempt, _) => Task.FromResult(attempt <= 3 ? new HttpResponseMessage((HttpStatusCode)status) : JsonResponse()));
        await Client(handler, time).GetWeather(0, 0, default);
        Assert.Equal(4, handler.Calls);
        Assert.Equal(new[] { 1d, 2d, 4d }, time.Delays.Select(delay => delay.TotalSeconds));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryAfter_DeltaAndDate_AreRespected(bool date)
    {
        var time = new FastTime();
        var handler = new Handler((attempt, _) =>
        {
            var response = attempt == 1 ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : JsonResponse();
            if (attempt == 1) response.Headers.RetryAfter = date ? new RetryConditionHeaderValue(new DateTimeOffset(Now.AddSeconds(7))) : new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
            return Task.FromResult(response);
        });
        await Client(handler, time).GetWeather(0, 0, default);
        Assert.Equal(TimeSpan.FromSeconds(7), Assert.Single(time.Delays));
    }

    [Fact]
    public async Task LongRetryAfter_StopsImmediatelyAndPreservesCooldown()
    {
        var handler = new Handler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(2));
            return Task.FromResult(response);
        });
        var error = await Assert.ThrowsAsync<OpenMeteoException>(() => Client(handler).GetWeather(0, 0, default));
        Assert.Equal(Now.AddHours(2), error.RetryAt);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("timeout")]
    [InlineData("server")]
    public async Task TransientFailure_HasBoundedRetries(string failure)
    {
        var handler = new Handler((_, _) => failure switch
        {
            "connection" => throw new HttpRequestException("Connection failed."),
            "timeout" => throw new TaskCanceledException("Timeout."),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError))
        });
        await Assert.ThrowsAsync<OpenMeteoException>(() => Client(handler).GetWeather(0, 0, default));
        Assert.Equal(4, handler.Calls);
    }

    [Fact]
    public async Task InvalidJsonAndClientErrors_AreNotRetried()
    {
        foreach (var response in new[] { new HttpResponseMessage(HttpStatusCode.BadRequest), new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid") } })
        {
            var handler = new Handler((_, _) => Task.FromResult(response));
            await Assert.ThrowsAsync<OpenMeteoException>(() => Client(handler).GetWeather(0, 0, default));
            Assert.Equal(1, handler.Calls);
        }
    }

    [Fact]
    public async Task CallerCancellation_DoesNotRetry()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new Handler((_, token) => { cancellation.Cancel(); token.ThrowIfCancellationRequested(); return Task.FromResult(JsonResponse()); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).GetWeather(0, 0, cancellation.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void InvalidConfiguration_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new OpenMeteoSettings { BaseUrl = "http://example.com" }.Validate());
        Assert.Throws<InvalidOperationException>(() => new OpenMeteoSettings { ForecastDays = 17 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new OpenMeteoSettings { MinimumRefreshIntervalMinutes = 61 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new OpenMeteoSettings { MaxRetryAttempts = 4 }.Validate());
        new OpenMeteoSettings().Validate();
    }

    private static OpenMeteoService Client(Handler handler, FastTime? time = null) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.open-meteo.com/v1/") }, Options.Create(new OpenMeteoSettings()), time ?? new FastTime(), NullLogger<OpenMeteoService>.Instance);
    private static HttpResponseMessage JsonResponse() => new(HttpStatusCode.OK) { Content = new StringContent(Snapshot(), Encoding.UTF8, "application/json") };
    private static string Snapshot() => """
        {"utc_offset_seconds":0,
         "current_units":{"time":"unixtime","temperature_2m":"°C","relative_humidity_2m":"%","apparent_temperature":"°C","wind_speed_10m":"km/h","precipitation":"mm","cloud_cover":"%","weather_code":"wmo code"},
         "hourly_units":{"time":"unixtime","temperature_2m":"°C","relative_humidity_2m":"%","apparent_temperature":"°C","wind_speed_10m":"km/h","precipitation_probability":"%","cloud_cover":"%","uv_index":"","shortwave_radiation":"W/m²"},
         "current":{"time":1794312000,"temperature_2m":30.5,"relative_humidity_2m":60,"apparent_temperature":34,"wind_speed_10m":10,"precipitation":null,"cloud_cover":20,"weather_code":1},
         "hourly":{"time":[1794312000,1794315600],"temperature_2m":[30,32],"relative_humidity_2m":[60,55],"apparent_temperature":[34,36],"precipitation_probability":[0,15],"wind_speed_10m":[10,12],"cloud_cover":[20,30],"uv_index":[6,8],"shortwave_radiation":[700,780]}}
        """;
    private sealed class Handler(Func<int, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Url { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; Url = request.RequestUri!.AbsoluteUri; return response(Calls, cancellationToken); }
    }
    private sealed class FastTime : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];
        public override DateTimeOffset GetUtcNow() => new(Now);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { Delays.Add(dueTime); return System.CreateTimer(callback, state, TimeSpan.FromMilliseconds(1), Timeout.InfiniteTimeSpan); }
    }
}
