using System;
using System.Collections.Generic;
using System.Text.Json;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Common;
using Xunit;

namespace FurniSpace.Application.Tests;

public class ApplicationTests
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
    public void Error_Conflict_SetsCodeMessageAndStatus()
    {
        var error = Error.Conflict("Product.SkuExists", "SKU already exists");

        Assert.Equal("Product.SkuExists", error.Code);
        Assert.Equal("SKU already exists", error.Message);
        Assert.Equal(409, error.Status);
    }

    [Fact]
    public void ServiceResult_Failure_UsesApplicationErrorStatusAndMessage()
    {
        var error = Error.Forbidden("User.Forbidden", "Access denied");

        var result = ServiceResult.Failure(error);

        Assert.Equal(403, result.Status);
        Assert.Equal("Access denied", result.Message);
    }

    [Fact]
    public void PagedResult_CalculatesPageMetadata()
    {
        var result = PagedResult<int>.Create(new[] { 21, 22 }, page: 3, pageSize: 10, totalItems: 22);

        Assert.Equal(3, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(22, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void JwtSettings_GetSecretKeyBytes_RejectsWeakSecret()
    {
        var settings = new JwtSettings { SecretKey = "short-secret" };

        Assert.Throws<InvalidOperationException>(() => settings.GetSecretKeyBytes());
    }

    [Fact]
    public void JwtSettings_GetSecretKeyBytes_AcceptsStrongUtf8Secret()
    {
        var settings = new JwtSettings { SecretKey = "this-secret-has-at-least-32-bytes" };

        var keyBytes = settings.GetSecretKeyBytes();

        Assert.True(keyBytes.Length >= JwtSettings.MinimumSecretKeyBytes);
    }
}
