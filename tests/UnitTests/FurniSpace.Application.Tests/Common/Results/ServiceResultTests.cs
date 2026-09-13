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

    [Fact]
    public void PayloadTooLarge_Uses413Status()
    {
        var result = ServiceResult<string>.PayloadTooLarge("Too big.");

        Assert.Equal(413, result.Status);
        Assert.Equal("Too big.", result.Message);
    }

    [Fact]
    public void UnsupportedMediaType_Uses415Status()
    {
        var result = ServiceResult.UnsupportedMediaType("Bad media.");

        Assert.Equal(415, result.Status);
        Assert.Equal("Bad media.", result.Message);
    }

    [Fact]
    public void Created_WithTypedData_Uses201Status()
    {
        var result = ServiceResult<int>.Created(10);

        Assert.Equal(201, result.Status);
        Assert.Equal(10, result.Data);
    }

    [Fact]
    public void Conflict_WithTypedData_Uses409Status()
    {
        var result = ServiceResult<string>.Conflict("Already exists.");

        Assert.Equal(409, result.Status);
    }

    [Fact]
    public void DefaultConstructor_UsesFailureDefaults()
    {
        var result = new ServiceResult<string>();

        Assert.Equal(-1, result.Status);
        Assert.Equal("Action failed", result.Message);
    }
}
