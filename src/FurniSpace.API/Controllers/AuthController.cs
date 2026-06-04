#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs;
using FurniSpace.Application.DTOs.Identity;
using FurniSpace.Application.Interfaces.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FurniSpace.API.Controllers;

[Route("auth")]
public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;
    private readonly IIdentityService _identityService;

    public AuthController(IAuthService authService, IIdentityService identityService)
    {
        _authService = authService;
        _identityService = identityService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _identityService.RegisterAsync(request, cancellationToken);
        SetRefreshTokenCookie(result.Data);
        return ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _identityService.LoginAsync(request, cancellationToken);
        SetRefreshTokenCookie(result.Data);
        return ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequestDto? request, CancellationToken cancellationToken)
    {
        request ??= new RefreshRequestDto();
        if (string.IsNullOrWhiteSpace(request.RefreshToken) &&
            Request.Cookies.TryGetValue("refresh_token", out var cookieToken))
        {
            request.RefreshToken = cookieToken;
        }

        var result = await _identityService.RefreshAsync(request, cancellationToken);
        SetRefreshTokenCookie(result.Data);
        return ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        return ToActionResult(await _identityService.ForgotPasswordAsync(request, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        return ToActionResult(await _identityService.ResetPasswordAsync(request, cancellationToken));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        return TryGetUserId(out var userId)
            ? ToActionResult(await _identityService.GetCurrentUserAsync(userId, cancellationToken))
            : ToActionResult(ServiceResult.Unauthorized());
    }

    [Authorize]
    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequestDto request, CancellationToken cancellationToken)
    {
        return TryGetUserId(out var userId)
            ? ToActionResult(await _identityService.UpdateProfileAsync(userId, request, cancellationToken))
            : ToActionResult(ServiceResult.Unauthorized());
    }

    [Authorize]
    [HttpPatch("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        return TryGetUserId(out var userId)
            ? ToActionResult(await _identityService.ChangePasswordAsync(userId, request, cancellationToken))
            : ToActionResult(ServiceResult.Unauthorized());
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequestDto? request, CancellationToken cancellationToken)
    {
        request ??= new LogoutRequestDto();
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return ToActionResult(ServiceResult.Unauthorized());
        }

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await _authService.RevokeRefreshTokenAsync(userId, request.RefreshToken, cancellationToken);
        }
        else if (Request.Cookies.TryGetValue("refresh_token", out var cookieToken))
        {
            await _authService.RevokeRefreshTokenAsync(userId, cookieToken, cancellationToken);
        }

        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var expiresAt = GetAccessTokenExpiresAt();

        if (!string.IsNullOrWhiteSpace(jti) && expiresAt.HasValue)
        {
            await _authService.RevokeAccessTokenAsync(jti, expiresAt.Value, cancellationToken);
        }

        Response.Cookies.Delete("refresh_token");
        return ToActionResult(ServiceResult.Success("Logged out successfully"));
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private void SetRefreshTokenCookie(AuthResponseDto? auth)
    {
        if (auth is null)
        {
            return;
        }

        Response.Cookies.Append("refresh_token", auth.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = auth.RefreshTokenExpiresAt,
            Path = "/"
        });
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
