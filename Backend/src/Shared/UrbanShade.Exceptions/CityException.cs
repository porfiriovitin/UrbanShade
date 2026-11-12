using UrbanShade.Exceptions.ExceptionsBase;

namespace UrbanShade.Exceptions;

public sealed class CityException : UrbanShadeBaseException
{
    private readonly ErrorKind _statusCode;
    private readonly string[] _messages;

    public CityException(ErrorKind statusCode, params string[] messages)
    {
        _statusCode = statusCode;
        _messages = messages;
    }

    public override ErrorKind GetErrorKind() => _statusCode;

    public override List<string> GetErrorMessages() => [.. _messages];
}
