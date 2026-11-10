namespace UrbanShade.Exceptions;

public sealed class EmailAlreadyRegisteredException : Exception
{
    public EmailAlreadyRegisteredException(Exception? innerException = null)
        : base("Email already registered.", innerException)
    {
    }
}
