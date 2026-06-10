#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Auth;
using FurniSpace.Application.DTOs.Identity;
using FurniSpace.Application.Interfaces.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FurniSpace.API.Controllers;

[Route("auth")]
public class AuthController : BaseApiController
{
    private const string AccessTokenCookieName = "access_token";
    private const string RefreshTokenCookieName = "refresh_token";

    private readonly IAuthService _authService;
    private readonly IIdentityService _identityService;
    private readonly IOptionsMonitor<CookieOptions> _authCookieOptions;

    public AuthController(
        IAuthService authService,
        IIdentityService identityService,
        IOptionsMonitor<CookieOptions> authCookieOptions)
    {
        _authService = authService;
        _identityService = identityService;
        _authCookieOptions = authCookieOptions;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-public")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _identityService.RegisterAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-public")]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _identityService.VerifyEmailAsync(request, cancellationToken);
        SetAuthCookies(result.Data);
        return ToActionResult(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-public")]
    [HttpPost("resend-verification-otp")]
    public async Task<IActionResult> ResendVerificationOtp(
        ResendVerificationOtpRequestDto request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await _identityService.ResendVerificationOtpAsync(request, cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-public")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _identityService.LoginAsync(request, cancellationToken);
        SetAuthCookies(result.Data);
        return ToActionResult(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-public")]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequestDto? request, CancellationToken cancellationToken)
    {
        request ??= new RefreshRequestDto();
        if (string.IsNullOrWhiteSpace(request.RefreshToken) &&
            Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken))
        {
            request.RefreshToken = cookieToken;
        }

        var result = await _identityService.RefreshAsync(request, cancellationToken);
        SetAuthCookies(result.Data);
        return ToActionResult(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-public")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        return ToActionResult(await _identityService.ForgotPasswordAsync(request, cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-public")]
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
        else if (Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken))
        {
            await _authService.RevokeRefreshTokenAsync(userId, cookieToken, cancellationToken);
        }

        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var expiresAt = GetAccessTokenExpiresAt();

        if (!string.IsNullOrWhiteSpace(jti) && expiresAt.HasValue)
        {
            await _authService.RevokeAccessTokenAsync(jti, expiresAt.Value, cancellationToken);
        }

        Response.Cookies.Delete(
            AccessTokenCookieName,
            GetAuthCookieOptions(AccessTokenCookieName));
        Response.Cookies.Delete(
            RefreshTokenCookieName,
            GetAuthCookieOptions(RefreshTokenCookieName));
        return ToActionResult(ServiceResult.Success("Logged out successfully"));
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private void SetAuthCookies(AuthResponseDto? auth)
    {
        if (auth is null)
        {
            return;
        }

        Response.Cookies.Append(
            AccessTokenCookieName,
            auth.AccessToken,
            GetAccessTokenCookieOptions(auth));

        Response.Cookies.Append(
            RefreshTokenCookieName,
            auth.RefreshToken,
            GetAuthCookieOptions(RefreshTokenCookieName));
    }

    private CookieOptions GetAccessTokenCookieOptions(AuthResponseDto auth)
    {
        var options = GetAuthCookieOptions(AccessTokenCookieName);
        options.Expires = auth.AccessTokenExpiresAt;
        return options;
    }

    private CookieOptions GetAuthCookieOptions(string cookieName)
    {
        var options = _authCookieOptions.Get(cookieName);
        return new CookieOptions
        {
            HttpOnly = options.HttpOnly,
            Secure = options.Secure,
            SameSite = options.SameSite,
            Path = options.Path,
            Domain = options.Domain,
            MaxAge = options.MaxAge,
            Expires = options.Expires,
            IsEssential = options.IsEssential
        };
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
