#nullable enable

using System;
using System.IdentityModel.Tokens.Jwt;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Services.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.Identity;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void GenerateTokenPair_IssuesAccessAndRefreshTokens()
    {
        var settings = Options.Create(new JwtSettings
        {
            SecretKey = "this-secret-has-at-least-32-bytes",
            Issuer = "FurniSpace",
            Audience = "FurniSpace",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        });
        var service = new JwtTokenService(settings);
        var userId = Guid.NewGuid();

        var result = service.GenerateTokenPair(userId, "user@example.com", "Test User", ["CUSTOMER"]);
        var accessToken = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.NotEqual(result.AccessToken, result.RefreshToken);
        Assert.Equal(userId.ToString(), accessToken.Subject);
        Assert.Contains(accessToken.Claims, claim => claim.Type == JwtRegisteredClaimNames.Jti);
        Assert.Contains(accessToken.Claims, claim => claim.Type == JwtRegisteredClaimNames.Iat);
        Assert.True(result.AccessTokenExpiresAt < result.RefreshTokenExpiresAt);
    }
}
