namespace UrbanShade.Domain.Security;

public sealed class FirstAccessSettings
{
    public int TemporaryPasswordHours { get; set; } = 24;
    public int TokenMinutes { get; set; } = 10;

    public void Validate()
    {
        if (TemporaryPasswordHours < 1 || TemporaryPasswordHours > 168 || TokenMinutes < 1 || TokenMinutes > 60)
            throw new InvalidOperationException("FirstAccess requires password validity between 1 and 168 hours and token validity between 1 and 60 minutes.");
    }
}
