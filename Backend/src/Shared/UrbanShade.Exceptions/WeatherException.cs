using UrbanShade.Exceptions.ExceptionsBase;

namespace UrbanShade.Exceptions;

public sealed class WeatherException(ErrorKind statusCode, params string[] messages) : UrbanShadeBaseException
{
    public override ErrorKind GetErrorKind() => statusCode;

    public override List<string> GetErrorMessages() => [.. messages];
}
