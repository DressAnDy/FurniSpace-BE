using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FurniSpace.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurniSpace.API.Tests;

public class ApiTests
{
    [Fact]
    public async Task CorrelationIdMiddleware_PreservesValidRequestHeader()
    {
        const string correlationId = "client-request-123";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(correlationId, context.Items[CorrelationIdMiddleware.ItemKey]);
        Assert.Equal(correlationId, context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_ReplacesUnsafeRequestHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "unsafe correlation id";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var correlationId = Assert.IsType<string>(context.Items[CorrelationIdMiddleware.ItemKey]);
        Assert.Equal(32, correlationId.Length);
        Assert.DoesNotContain(' ', correlationId);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_ReturnsCorrelationId()
    {
        const string correlationId = "server-request-123";
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.ItemKey] = correlationId;

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Test exception"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(correlationId, context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
        Assert.Equal(correlationId, response.RootElement.GetProperty("correlationId").GetString());
    }
}
