using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.Common;

namespace PersonalFinance.Api.Accounts;

public sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not RequestValidationException validationException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problem = new ValidationProblemDetails
        {
            Title = "The request contains invalid input.",
            Status = StatusCodes.Status400BadRequest,
            Errors = validationException.Errors.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
