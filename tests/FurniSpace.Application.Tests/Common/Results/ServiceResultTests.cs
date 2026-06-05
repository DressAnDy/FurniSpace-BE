#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using FurniSpace.Application.Common;
using Xunit;

namespace FurniSpace.Application.Tests.Common.Results;

public sealed class ServiceResultTests
{
    [Fact]
    public void Success_WithTypedData_SetsSuccessResponse()
    {
        var result = ServiceResult<string>.Success("payload");

        Assert.Equal(200, result.Status);
        Assert.Equal("Success", result.Message);
        Assert.Equal("payload", result.Data);
        Assert.Null(result.Errors);
    }

    [Fact]
    public void BadRequest_WithErrors_SetsValidationResponse()
    {
        var errors = new List<string> { "Email is required" };

        var result = ServiceResult.BadRequest(errors);

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Equal(errors, result.Errors);
    }

    [Fact]
    public void Serialize_WhenErrorsAreNull_OmitsErrors()
    {
        var result = ServiceResult.Success(new { Id = 1 });

        var json = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("\"Errors\"", json);
    }

    [Fact]
    public void Failure_UsesApplicationErrorStatusAndMessage()
    {
        var error = Error.Forbidden("User.Forbidden", "Access denied");

        var result = ServiceResult.Failure(error);

        Assert.Equal(403, result.Status);
        Assert.Equal("Access denied", result.Message);
    }

    [Fact]
    public void TooManyRequests_Uses429Status()
    {
        var result = ServiceResult.TooManyRequests("Slow down.");

        Assert.Equal(429, result.Status);
        Assert.Equal("Slow down.", result.Message);
    }
}
