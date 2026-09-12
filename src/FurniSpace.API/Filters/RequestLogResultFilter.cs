using FurniSpace.API.Logging;
using FurniSpace.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FurniSpace.API.Filters;

public sealed class RequestLogResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        switch (context.Result)
        {
            case ObjectResult { Value: IServiceResult result }:
                RequestLogSummary.Set(context.HttpContext, result);
                break;
            case ObjectResult { Value: ProblemDetails problem }:
                RequestLogSummary.Set(context.HttpContext, problem.Title ?? problem.Detail, null);
                break;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
