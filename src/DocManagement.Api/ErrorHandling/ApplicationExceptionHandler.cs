using DocManagement.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DocManagement.Api.ErrorHandling;

internal sealed class ApplicationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Title)? problem = exception switch
        {
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "The document was modified by another request. Reload it and try again."),
            PreviewQueueUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Preview generation is temporarily unavailable. Try again later."),
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Value.Status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = problem.Value.Status,
                Title = problem.Value.Title,
            },
        });
    }
}
