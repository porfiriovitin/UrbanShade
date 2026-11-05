using System.Net;

namespace UrbanShade.Exceptions.ExceptionsBase;

public abstract class UrbanShadeBaseException : Exception
{
    public abstract HttpStatusCode GetStatusCode();
    public abstract List<string> GetErrorMessages();
}
