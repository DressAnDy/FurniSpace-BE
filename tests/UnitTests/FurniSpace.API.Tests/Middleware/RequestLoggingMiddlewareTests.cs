using FurniSpace.API.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using Xunit;

namespace FurniSpace.API.Tests.Middleware;

public sealed class RequestLoggingMiddlewareTests
{
    [Theory]
    [InlineData("POST", "/auth/login", 200, 500, LogEventLevel.Debug)]
    [InlineData("POST", "/auth/login", 200, 1_000, LogEventLevel.Warning)]
    [InlineData("GET", "/hubs/notifications", 200, 30_000, LogEventLevel.Debug)]
    [InlineData("GET", "/hubs/notifications", 401, 10, LogEventLevel.Warning)]
    [InlineData("GET", "/projects", 200, 167, LogEventLevel.Debug)]
    [InlineData("GET", "/projects", 200, 300, LogEventLevel.Information)]
    [InlineData("POST", "/auth/logout", 200, 5, LogEventLevel.Information)]
    [InlineData("GET", "/projects", 500, 10, LogEventLevel.Error)]
    public void GetLogLevel_ClassifiesExpectedAndActionableRequests(
        string method,
        string path,
        int statusCode,
        long elapsedMilliseconds,
        LogEventLevel expected)
    {
        var actual = RequestLoggingMiddleware.GetLogLevel(
            method,
            new PathString(path),
            statusCode,
            elapsedMilliseconds);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FormatRequestLog_IncludesApiCodeMessageAndTime()
    {
        var actual = RequestLoggingMiddleware.FormatRequestLog(
            "POST",
            "/auth/login",
            200,
            errorCode: null,
            "Logged in successfully.",
            4310.4);

        Assert.Equal(
            """
            API      POST /auth/login
                           Code     200
                           Message  Logged in successfully.
                           Time     4310ms
            """,
            actual);
    }

    [Fact]
    public void FormatRequestLog_OmitsEmptyMessageAndAppendsErrorCode()
    {
        var actual = RequestLoggingMiddleware.FormatRequestLog(
            "GET",
            "/products/search",
            400,
            "INVALID_BUSINESS_TYPE_FILTER",
            message: null,
            12);

        Assert.Equal(
            """
            API      GET /products/search
                           Code     400 INVALID_BUSINESS_TYPE_FILTER
                           Time     12ms
            """,
            actual);
    }
}
