#nullable enable

using System;
using System.Text.Json;
using FurniSpace.Application.DTOs.Auth;
using Xunit;

namespace FurniSpace.Application.Tests.Identity;

public sealed class AuthResponseDtoTests
{
    [Fact]
    public void Serialize_UsesSnakeCaseAndDoesNotExposeTokens()
    {
        var response = new AuthResponseDto
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        var json = JsonSerializer.Serialize(response);

        Assert.DoesNotContain("\"access_token\":\"access\"", json);
        Assert.DoesNotContain("\"access\"", json);
        Assert.DoesNotContain("refresh_token", json);
        Assert.DoesNotContain("refresh", json);
        Assert.Contains("\"expires_in\":", json);
    }
}
