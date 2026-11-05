using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Responses;
using UrbanShade.Exceptions.ExceptionsBase;

namespace UrbanShade.API.Filters;

public class ExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        int statusCode;
        List<string> errorMessages = [];

        if (context.Exception is UrbanShadeBaseException baseException)
        {
            statusCode = (int)baseException.GetStatusCode();
            errorMessages = baseException.GetErrorMessages();
        }
        else
        {
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