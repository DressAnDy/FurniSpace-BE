#nullable enable

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Auth;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Auth;
using FurniSpace.Application.DTOs.Identity;
using FurniSpace.Application.Interfaces.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task Login_ReturnsResultAndSetsAuthCookies()
    {
        var auth = CreateAuthResponse();
        var identity = new FakeIdentityService
        {
            LoginResult = ServiceResult<AuthResponseDto>.Success(auth, "Logged in")
        };
        var controller = CreateController(identity: identity);

        var result = await controller.Login(new LoginRequestDto { Email = "a@example.com", Password = "secret" }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        var setCookie = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains("access_token=access-token", setCookie);
        Assert.Contains("refresh_token=refresh-token", setCookie);
    }

    [Fact]
    public async Task PublicIdentityActions_ReturnServiceResults()
    {
        var identity = new FakeIdentityService();
        var controller = CreateController(identity: identity);

        var register = await controller.Register(new RegisterRequestDto(), CancellationToken.None);
        var resend = await controller.ResendVerificationOtp(new ResendVerificationOtpRequestDto(), CancellationToken.None);
        var forgot = await controller.ForgotPassword(new ForgotPasswordRequestDto(), CancellationToken.None);
        var reset = await controller.ResetPassword(new ResetPasswordRequestDto(), CancellationToken.None);

        Assert.Equal(200, Assert.IsType<ObjectResult>(register).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(resend).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(forgot).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(reset).StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_ReturnsResultAndSetsAuthCookies()
    {
        var identity = new FakeIdentityService();
        var controller = CreateController(identity: identity);

        var result = await controller.VerifyEmail(new VerifyEmailRequestDto(), CancellationToken.None);

        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Contains("access_token=access-token", controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task Refresh_WhenRequestMissingToken_UsesRefreshCookie()
    {
        var identity = new FakeIdentityService
        {
            RefreshResult = ServiceResult<AuthResponseDto>.Success(CreateAuthResponse(), "Refreshed")
        };
        var controller = CreateController(identity: identity);
        controller.Request.Headers.Cookie = "refresh_token=from-cookie";

        var result = await controller.Refresh(null, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        Assert.Equal("from-cookie", identity.RefreshRequest?.RefreshToken);
    }

    [Fact]
    public async Task Me_WithValidUserId_ReturnsCurrentUser()
    {
        var userId = Guid.NewGuid();
        var identity = new FakeIdentityService
        {
            CurrentUserResult = ServiceResult<CurrentUserDto>.Success(new CurrentUserDto
            {
                AccountId = userId,
                Email = "a@example.com",
                FullName = "Nguyen Van A"
            })
        };
        var controller = CreateController(identity: identity);
        SetUser(controller, userId);

        var result = await controller.Me(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(userId, identity.CurrentUserId);
    }

    [Fact]
    public async Task Me_WithMissingUserId_ReturnsUnauthorized()
    {
        var controller = CreateController();

        var result = await controller.Me(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, objectResult.StatusCode);
    }

    [Fact]
    public async Task ProfileActions_WithValidUserId_DelegateToIdentityService()
    {
        var userId = Guid.NewGuid();
        var identity = new FakeIdentityService();
        var controller = CreateController(identity: identity);
        SetUser(controller, userId);

        var update = await controller.UpdateMe(new UpdateProfileRequestDto(), CancellationToken.None);
        var changePassword = await controller.ChangePassword(new ChangePasswordRequestDto(), CancellationToken.None);

        Assert.Equal(200, Assert.IsType<ObjectResult>(update).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(changePassword).StatusCode);
        Assert.Equal(userId, identity.UpdatedUserId);
        Assert.Equal(userId, identity.PasswordChangedUserId);
    }

    [Fact]
    public async Task ProfileActions_WithMissingUserId_ReturnUnauthorized()
    {
        var controller = CreateController();

        var update = await controller.UpdateMe(new UpdateProfileRequestDto(), CancellationToken.None);
        var changePassword = await controller.ChangePassword(new ChangePasswordRequestDto(), CancellationToken.None);

        Assert.Equal(401, Assert.IsType<ObjectResult>(update).StatusCode);
        Assert.Equal(401, Assert.IsType<ObjectResult>(changePassword).StatusCode);
    }

    [Fact]
    public async Task Logout_WithRequestTokenAndAccessTokenClaims_RevokesTokensAndDeletesCookies()
    {
        var userId = Guid.NewGuid();
        var auth = new FakeAuthService();
        var controller = CreateController(auth);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        SetUser(controller, userId, jti: "jti-1", expiresAt);

        var result = await controller.Logout(new LogoutRequestDto { RefreshToken = "request-refresh" }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(userId, auth.RefreshRevokedUserId);
        Assert.Equal("request-refresh", auth.RefreshRevokedToken);
        Assert.Equal("jti-1", auth.AccessRevokedJti);
        Assert.Equal(expiresAt.ToUnixTimeSeconds(), auth.AccessRevokedExpiresAt?.ToUnixTimeSeconds());
        Assert.Contains("access_token=", controller.Response.Headers.SetCookie.ToString());
        Assert.Contains("refresh_token=", controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task Logout_WithInvalidUserId_ReturnsUnauthorized()
    {
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
                ]))
            }
        };

        var result = await controller.Logout(null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, objectResult.StatusCode);
    }

    private static AuthController CreateController(
        FakeAuthService? auth = null,
        FakeIdentityService? identity = null)
    {
        return new AuthController(
            auth ?? new FakeAuthService(),
            identity ?? new FakeIdentityService(),
            new FakeCookieOptionsMonitor())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static AuthResponseDto CreateAuthResponse()
        => new()
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

    private static void SetUser(AuthController controller, Guid userId, string? jti = null, DateTimeOffset? expiresAt = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(jti))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        }

        if (expiresAt.HasValue)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Exp, expiresAt.Value.ToUnixTimeSeconds().ToString()));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"))
            }
        };
    }

    private sealed class FakeCookieOptionsMonitor : IOptionsMonitor<CookieOptions>
    {
        public CookieOptions CurrentValue => Get(string.Empty);

        public CookieOptions Get(string? name)
            => new()
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true
            };

        public IDisposable? OnChange(Action<CookieOptions, string?> listener)
            => null;
    }

    private sealed class FakeAuthService : IAuthService
    {
        public Guid? RefreshRevokedUserId { get; private set; }
        public string? RefreshRevokedToken { get; private set; }
        public string? AccessRevokedJti { get; private set; }
        public DateTimeOffset? AccessRevokedExpiresAt { get; private set; }

        public Task RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
        {
            RefreshRevokedUserId = userId;
            RefreshRevokedToken = refreshToken;
            return Task.CompletedTask;
        }

        public Task RevokeAccessTokenAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
        {
            AccessRevokedJti = jti;
            AccessRevokedExpiresAt = expiresAt;
            return Task.CompletedTask;
        }

        public Task<AuthResponseDto> CreateSessionAsync(Guid userId, string email, string fullName, IEnumerable<string>? roles = null, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateAuthResponse());
        public Task<bool> ValidateRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public Task<AuthResponseDto?> RotateRefreshTokenAsync(Guid userId, string refreshToken, string email, string fullName, IEnumerable<string>? roles = null, CancellationToken cancellationToken = default)
            => Task.FromResult<AuthResponseDto?>(CreateAuthResponse());
        public Task RevokeUserAccessTokensAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<bool> IsAccessTokenRevokedAsync(string jti, Guid userId, DateTimeOffset issuedAt, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        public ServiceResult<AuthResponseDto> LoginResult { get; set; } = ServiceResult<AuthResponseDto>.Success(CreateAuthResponse());
        public ServiceResult<AuthResponseDto> RefreshResult { get; set; } = ServiceResult<AuthResponseDto>.Success(CreateAuthResponse());
        public ServiceResult<CurrentUserDto> CurrentUserResult { get; set; } = ServiceResult<CurrentUserDto>.Success(new CurrentUserDto());
        public RefreshRequestDto? RefreshRequest { get; private set; }
        public Guid? CurrentUserId { get; private set; }
        public Guid? UpdatedUserId { get; private set; }
        public Guid? PasswordChangedUserId { get; private set; }

        public Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(LoginResult);

        public Task<ServiceResult<AuthResponseDto>> RefreshAsync(RefreshRequestDto request, CancellationToken cancellationToken = default)
        {
            RefreshRequest = request;
            return Task.FromResult(RefreshResult);
        }

        public Task<ServiceResult<CurrentUserDto>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            CurrentUserId = userId;
            return Task.FromResult(CurrentUserResult);
        }

        public Task<ServiceResult> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult.Success());
        public Task<ServiceResult<AuthResponseDto>> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<AuthResponseDto>.Success(CreateAuthResponse()));
        public Task<ServiceResult> ResendVerificationOtpAsync(ResendVerificationOtpRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult.Success());
        public Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult.Success());
        public Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult.Success());
        public Task<ServiceResult<CurrentUserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
        {
            UpdatedUserId = userId;
            return Task.FromResult(CurrentUserResult);
        }

        public Task<ServiceResult> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
        {
            PasswordChangedUserId = userId;
            return Task.FromResult(ServiceResult.Success());
        }
    }
}
