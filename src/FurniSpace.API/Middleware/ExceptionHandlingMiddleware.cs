using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing HTTP {RequestMethod} {RequestPath}",
                context.Request.Method,
                context.Request.Path.Value);

            if (context.Response.HasStarted)
            {
                throw;
            }

            var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
                ?? context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The server could not complete the request.",
                Instance = context.Request.Path,
                Extensions =
                {
                    ["correlationId"] = correlationId
                }
            });
        }
    }
}
