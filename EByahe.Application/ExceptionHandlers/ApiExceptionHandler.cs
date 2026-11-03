using EByahe.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using EByahe.Domain.Exceptions;

namespace EByahe.Application.ExceptionHandlers;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
  {
    if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested) return false;
    if (exception is DocumentStorageException)
    {
      context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
      await context.Response.WriteAsJsonAsync(new ProblemDetails
      {
        Status = StatusCodes.Status503ServiceUnavailable,
        Title = "Document storage is unavailable. Please try again later."
      }, cancellationToken);
      return true;
    }
    if (exception is PartnerApplicationValidationException validation)
    {
      context.Response.StatusCode = StatusCodes.Status400BadRequest;
      await context.Response.WriteAsJsonAsync(new ValidationProblemDetails(validation.Errors)
      {
        Status = StatusCodes.Status400BadRequest, Title = "One or more validation errors occurred."
      }, cancellationToken);
      return true;
    }
    logger.LogError("Request {TraceId} failed with {ErrorType}.", context.TraceIdentifier, exception.GetType().Name);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
      Status = StatusCodes.Status500InternalServerError, Title = "The request could not be completed.",
      Extensions = { ["traceId"] = context.TraceIdentifier }
    }, cancellationToken);
    return true;
  }
}
