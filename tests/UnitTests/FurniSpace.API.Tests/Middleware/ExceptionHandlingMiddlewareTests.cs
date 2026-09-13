#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FurniSpace.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurniSpace.API.Tests.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsCorrelationId()
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
