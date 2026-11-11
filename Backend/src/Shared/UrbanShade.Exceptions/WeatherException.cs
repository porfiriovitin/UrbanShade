using System.Net;
using UrbanShade.Exceptions.ExceptionsBase;

namespace UrbanShade.Exceptions;

public sealed class WeatherException(HttpStatusCode statusCode, params string[] messages) : UrbanShadeBaseException
{
    public override HttpStatusCode GetStatusCode() => statusCode;
    public override List<string> GetErrorMessages() => [.. messages];
}
