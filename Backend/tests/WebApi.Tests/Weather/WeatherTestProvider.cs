using UrbanShade.Domain.Weather;

namespace WebApi.Tests.Weather;

public sealed class WeatherTestProvider : IOpenMeteoService
{
    public int Calls { get; private set; }
    public bool Fail { get; set; }
    public int FailNext { get; set; }
    public bool Block { get; set; }
    public decimal Temperature { get; set; } = 30;
    public DateTime? RetryAt { get; set; }
    public TaskCompletionSource Entered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DateTime _forecastStart = DateTime.UtcNow.Date.AddHours(DateTime.UtcNow.Hour + 1);

    public void Reset()
    {
        Calls = 0; Fail = false; FailNext = 0; Block = false; Temperature = 30; RetryAt = null;
        Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    public async Task<WeatherObservation> GetWeather(double latitude, double longitude, CancellationToken cancellationToken)
    {
        Calls++;
        Entered.TrySetResult();
        if (Block) await Release.Task.WaitAsync(cancellationToken);
        if (Fail || FailNext-- > 0) throw new OpenMeteoException("Weather test provider is unavailable.", RetryAt);
        var now = DateTime.UtcNow;
        return new(now.AddMinutes(-5), now, new(Temperature, 60, 34, 8, 0, 20, 1),
            Enumerable.Range(0, 72).Select(hour => new WeatherPrediction(_forecastStart.AddHours(hour), Temperature, 60, 34, 15, 8, 20, 8, 780)).ToList());
    }
}
