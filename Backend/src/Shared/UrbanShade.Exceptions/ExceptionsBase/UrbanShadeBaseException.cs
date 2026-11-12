
namespace UrbanShade.Exceptions.ExceptionsBase;

public abstract class UrbanShadeBaseException : Exception
{
    public abstract ErrorKind GetErrorKind();
    public abstract List<string> GetErrorMessages();
}
