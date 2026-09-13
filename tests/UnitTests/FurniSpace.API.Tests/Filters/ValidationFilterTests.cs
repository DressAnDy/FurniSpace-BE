#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using FurniSpace.API.Filters;
using FurniSpace.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace FurniSpace.API.Tests.Filters;

public sealed class ValidationFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_WithValidModelState_CallsNext()
    {
        var filter = new ValidationFilter();
        var context = CreateContext();
        var nextCalled = false;

        await filter.OnActionExecutionAsync(
            context,
            () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(
                    context,
                    filters: [],
                    controller: new object()));
            });

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithInvalidModelState_ReturnsBadRequestErrors()
    {
        var filter = new ValidationFilter();
        var context = CreateContext();
        context.ModelState.AddModelError("Email", "Email is required.");
        context.ModelState.AddModelError("Password", string.Empty);
        context.ModelState.AddModelError(string.Empty, string.Empty);

        await filter.OnActionExecutionAsync(
            context,
            () => throw new Xunit.Sdk.XunitException("Next should not be called."));

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        var serviceResult = Assert.IsType<ServiceResult>(result.Value);
        Assert.Equal(400, serviceResult.Status);
        Assert.NotNull(serviceResult.Errors);
        Assert.Contains("Email is required.", serviceResult.Errors);
        Assert.Contains("Password is invalid.", serviceResult.Errors);
        Assert.Contains("The request payload is invalid.", serviceResult.Errors);
    }

    private static ActionExecutingContext CreateContext()
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());

        return new ActionExecutingContext(
            actionContext,
            filters: [],
            actionArguments: new Dictionary<string, object?>(),
            controller: new object());
    }
}
