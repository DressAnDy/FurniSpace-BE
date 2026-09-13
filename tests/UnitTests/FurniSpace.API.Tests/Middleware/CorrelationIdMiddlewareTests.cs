#nullable enable

using System.Threading.Tasks;
using FurniSpace.API.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace FurniSpace.API.Tests.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_PreservesValidRequestHeader()
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
    public async Task InvokeAsync_ReplacesUnsafeRequestHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "unsafe correlation id";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var correlationId = Assert.IsType<string>(context.Items[CorrelationIdMiddleware.ItemKey]);
        Assert.Equal(32, correlationId.Length);
        Assert.DoesNotContain(' ', correlationId);
    }
}
