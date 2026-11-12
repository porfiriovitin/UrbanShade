namespace UrbanShade.Exceptions;

public enum ErrorKind
{
    Validation,
    Unauthenticated,
    Forbidden,
    NotFound,
    Conflict,
    Unavailable,
    RateLimited,
    InvalidUpstreamResponse,
    Unprocessable
}
