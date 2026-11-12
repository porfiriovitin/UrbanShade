using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Responses;
using UrbanShade.Exceptions;
using UrbanShade.Exceptions.ExceptionsBase;

namespace UrbanShade.API.Filters;

public class ExceptionFilter(ILogger<ExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        int statusCode;
        List<string> errorMessages = [];

        if (context.Exception is EmailAlreadyRegisteredException emailException)
        {
            statusCode = StatusCodes.Status409Conflict;
            errorMessages.Add(emailException.Message);
        }
        else if (context.Exception is UrbanShadeBaseException baseException)
        {
            statusCode = baseException.GetErrorKind() switch
            {
                ErrorKind.Validation => (int)System.Net.HttpStatusCode.BadRequest,
                ErrorKind.Unauthenticated => (int)System.Net.HttpStatusCode.Unauthorized,
                ErrorKind.Forbidden => (int)System.Net.HttpStatusCode.Forbidden,
                ErrorKind.NotFound => (int)System.Net.HttpStatusCode.NotFound,
                ErrorKind.Conflict => (int)System.Net.HttpStatusCode.Conflict,
                ErrorKind.Unavailable => (int)System.Net.HttpStatusCode.ServiceUnavailable,
                ErrorKind.RateLimited => (int)System.Net.HttpStatusCode.TooManyRequests,
                ErrorKind.InvalidUpstreamResponse => (int)System.Net.HttpStatusCode.BadGateway,
                ErrorKind.Unprocessable => (int)System.Net.HttpStatusCode.UnprocessableEntity,
                _ => StatusCodes.Status500InternalServerError
            };
            errorMessages = baseException.GetErrorMessages();
        }
        else
        {
            logger.LogError(context.Exception, "Unhandled request failure {TraceId} at {Path}", context.HttpContext.TraceIdentifier, context.HttpContext.Request.Path);
            statusCode = StatusCodes.Status500InternalServerError;
            errorMessages.Add("An unexpected error occurred.");
        }

        var payload = new PayloadResponse
        {
            Status = nameof(ResponseStatus.Error),
            Messages = errorMessages
        };

        context.Result = new ObjectResult(payload)
        {
            StatusCode = statusCode
        };

        context.ExceptionHandled = true;

    }
}
