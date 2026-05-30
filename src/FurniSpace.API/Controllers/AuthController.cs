using FurniSpace.API.Base;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs;
using FurniSpace.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FurniSpace.API.Controllers;

public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequestDto request, CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return ToActionResult(ServiceResult.Unauthorized());
        }

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await _authService.RevokeRefreshTokenAsync(userId, request.RefreshToken, cancellationToken);
        }

        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var expiresAt = GetAccessTokenExpiresAt();

        if (!string.IsNullOrWhiteSpace(jti) && expiresAt.HasValue)
        {
            await _authService.RevokeAccessTokenAsync(jti, expiresAt.Value, cancellationToken);
        }

        return ToActionResult(ServiceResult.Success("Logged out successfully"));
    }

    private DateTimeOffset? GetAccessTokenExpiresAt()
    {
        var exp = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        if (!long.TryParse(exp, out var unixSeconds))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }
}
