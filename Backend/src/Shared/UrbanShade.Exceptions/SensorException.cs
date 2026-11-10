using System.Net;
using UrbanShade.Exceptions.ExceptionsBase;

namespace UrbanShade.Exceptions;

public sealed class SensorException : UrbanShadeBaseException
{
    private readonly HttpStatusCode _statusCode;
    private readonly string[] _messages;

    public SensorException(HttpStatusCode statusCode, params string[] messages)
    {
        _statusCode = statusCode;
        _messages = messages;
    }

    public override HttpStatusCode GetStatusCode() => _statusCode;
    public override List<string> GetErrorMessages() => [.. _messages];
}
