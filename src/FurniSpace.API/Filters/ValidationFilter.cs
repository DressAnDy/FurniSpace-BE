using FurniSpace.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FurniSpace.API.Filters;

public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ModelState.IsValid)
        {
            await next();
            return;
        }

        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error => FormatValidationError(entry.Key, error)))
            .ToList();

        context.Result = new ObjectResult(ServiceResult.BadRequest(errors))
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    private static string FormatValidationError(string key, ModelError error)
    {
        if (!string.IsNullOrWhiteSpace(error.ErrorMessage))
        {
            return error.ErrorMessage;
        }

        return string.IsNullOrWhiteSpace(key)
            ? "The request payload is invalid."
            : $"{key} is invalid.";
    }
}
