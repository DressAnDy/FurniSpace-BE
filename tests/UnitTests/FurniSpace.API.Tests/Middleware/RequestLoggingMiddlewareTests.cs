using FurniSpace.API.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using Xunit;

namespace FurniSpace.API.Tests.Middleware;

public sealed class RequestLoggingMiddlewareTests
{
    [Theory]
    [InlineData("/auth/login", 200, 500, LogEventLevel.Debug)]
    [InlineData("/auth/login", 200, 1_000, LogEventLevel.Warning)]
    [InlineData("/hubs/notifications", 200, 30_000, LogEventLevel.Debug)]
    [InlineData("/hubs/notifications", 401, 10, LogEventLevel.Warning)]
    [InlineData("/projects", 200, 500, LogEventLevel.Information)]
    [InlineData("/projects", 500, 10, LogEventLevel.Error)]
    public void GetLogLevel_ClassifiesExpectedAndActionableRequests(
        string path,
        int statusCode,
        long elapsedMilliseconds,
        LogEventLevel expected)
    {
        var actual = RequestLoggingMiddleware.GetLogLevel(
            new PathString(path),
            statusCode,
            elapsedMilliseconds);

        Assert.Equal(expected, actual);
    }
}
