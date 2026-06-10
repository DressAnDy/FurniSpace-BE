using System.Security.Cryptography;
using System.Text;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Auth;
using FurniSpace.Application.DTOs.Identity;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.AspNetCore.Identity;
using InfrastructureCacheService = FurniSpace.Infrastructure.Interfaces.ICacheService;

namespace FurniSpace.Application.Services.Identity;

public sealed class IdentityService : IIdentityService
{
    private const string CustomerRole = "CUSTOMER";
    private static readonly TimeSpan EmailRateLimitWindow = TimeSpan.FromMinutes(5);
    private readonly IAccountRepository _accounts;
    private readonly IAuthService _auth;
    private readonly IPasswordHasher<Account> _passwordHasher;
    private readonly IPasswordResetStore _passwordResetStore;
    private readonly IEmailOtpStore _emailOtpStore;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IEmailService _email;
    private readonly InfrastructureCacheService _cache;

    public IdentityService(
        IAccountRepository accounts,
        IAuthService auth,
        IPasswordHasher<Account> passwordHasher,
        IPasswordResetStore passwordResetStore,
        IEmailOtpStore emailOtpStore,
        IRefreshTokenStore refreshTokens,
        IEmailService email,
        InfrastructureCacheService cache)
    {
        _accounts = accounts;
        _auth = auth;
        _passwordHasher = passwordHasher;
        _passwordResetStore = passwordResetStore;
        _emailOtpStore = emailOtpStore;
        _refreshTokens = refreshTokens;
        _email = email;
        _cache = cache;
    }

    public async Task<ServiceResult> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateRegistration(request);
        if (errors.Count > 0)
        {
            return ServiceResult.BadRequest(errors);
        }

        var email = NormalizeEmail(request.Email);
        if (!await AllowEmailAttemptAsync("register", email, 5, cancellationToken))
        {
            return ServiceResult.TooManyRequests("Too many registration attempts. Please try again later.");
        }

        if (await _accounts.EmailExistsAsync(email, cancellationToken: cancellationToken))
        {
            return ServiceResult.Conflict("Email already exists.");
        }

        var customerRoleId = await _accounts.GetRoleIdByNameAsync(CustomerRole, cancellationToken);
        if (!customerRoleId.HasValue)
        {
            return ServiceResult.InternalServerError("Customer role is not configured.");
        }

        var account = new Account
        {
            AccountId = Guid.NewGuid(),
            RoleId = customerRoleId.Value,
            Email = email,
            FullName = request.FullName.Trim(),
            Phone = NormalizeOptional(request.Phone),
            Status = AccountStatus.INACTIVE
        };
        account.PasswordHash = _passwordHasher.HashPassword(account, request.Password);

        await _accounts.AddAsync(account, cancellationToken);
        await _accounts.SaveChangesAsync(cancellationToken);
        await SendVerificationOtpAsync(account, cancellationToken);

        return ServiceResult.Created(new { account.AccountId, account.Email }, "Account registered. Please verify your email with the OTP sent to your inbox.");
    }

    public async Task<ServiceResult<AuthResponseDto>> VerifyEmailAsync(
        VerifyEmailRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.OtpCode))
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Email and OTP code are required.");
        }

        var email = NormalizeEmail(request.Email);
        if (!await AllowEmailAttemptAsync("verify-email", email, 10, cancellationToken))
        {
            return ServiceResult<AuthResponseDto>.TooManyRequests("Too many verification attempts. Please try again later.");
        }

        var account = await _accounts.GetByEmailAsync(email, cancellationToken);
        if (account is null || account.DeletedAt is not null)
        {
            return ServiceResult<AuthResponseDto>.BadRequest("OTP code is invalid or expired.");
        }

        if (account.Status == AccountStatus.ACTIVE)
        {
            return ServiceResult<AuthResponseDto>.Conflict("Email is already verified.");
        }

        if (!await _emailOtpStore.ConsumeAsync(email, request.OtpCode, cancellationToken))
        {
            return ServiceResult<AuthResponseDto>.BadRequest("OTP code is invalid or expired.");
        }

        account.Status = AccountStatus.ACTIVE;
        await _accounts.SaveChangesAsync(cancellationToken);

        var role = await _accounts.GetRoleNameAsync(account.RoleId, cancellationToken);
        var session = await _auth.CreateSessionAsync(
            account.AccountId,
            account.Email,
            account.FullName,
            RoleList(role),
            cancellationToken);

        return ServiceResult<AuthResponseDto>.Success(session, "Email verified successfully.");
    }

    public async Task<ServiceResult> ResendVerificationOtpAsync(
        ResendVerificationOtpRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ServiceResult.BadRequest("Email is required.");
        }

        var email = NormalizeEmail(request.Email);
        if (!await AllowEmailAttemptAsync("resend-verification", email, 3, cancellationToken))
        {
            return ServiceResult.TooManyRequests("Too many OTP requests. Please try again later.");
        }

        var account = await _accounts.GetByEmailAsync(email, cancellationToken);
        if (account is not null && account.DeletedAt is null && account.Status != AccountStatus.ACTIVE)
        {
            await SendVerificationOtpAsync(account, cancellationToken);
        }

        return ServiceResult.Success("If the account requires verification, an OTP has been sent.");
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Email and password are required.");
        }

        var email = NormalizeEmail(request.Email);
        if (!await AllowEmailAttemptAsync("login", email, 10, cancellationToken))
        {
            return ServiceResult<AuthResponseDto>.TooManyRequests("Too many login attempts. Please try again later.");
        }

        var account = await _accounts.GetByEmailAsync(email, cancellationToken);
        if (!CanAuthenticate(account))
        {
            return ServiceResult<AuthResponseDto>.Unauthorized("Email or password is invalid.");
        }

        var activeAccount = account!;
        var verificationResult = _passwordHasher.VerifyHashedPassword(
            activeAccount,
            activeAccount.PasswordHash,
            request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return ServiceResult<AuthResponseDto>.Unauthorized("Email or password is invalid.");
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            activeAccount.PasswordHash = _passwordHasher.HashPassword(activeAccount, request.Password);
            await _accounts.SaveChangesAsync(cancellationToken);
        }

        var role = await _accounts.GetRoleNameAsync(activeAccount.RoleId, cancellationToken);
        var session = await _auth.CreateSessionAsync(
            activeAccount.AccountId,
            activeAccount.Email,
            activeAccount.FullName,
            RoleList(role),
            cancellationToken);
        return ServiceResult<AuthResponseDto>.Success(session, "Logged in successfully.");
    }

    public async Task<ServiceResult<AuthResponseDto>> RefreshAsync(
        RefreshRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Refresh token is required.");
        }

        var userId = await _refreshTokens.ResolveUserIdAsync(request.RefreshToken, cancellationToken);
        if (!userId.HasValue)
        {
            return ServiceResult<AuthResponseDto>.Unauthorized("Refresh token is invalid or expired.");
        }

        var account = await _accounts.GetByIdAsync(userId.Value, cancellationToken);
        if (!CanAuthenticate(account))
        {
            return ServiceResult<AuthResponseDto>.Unauthorized("Refresh token is invalid or expired.");
        }

        var role = await _accounts.GetRoleNameAsync(account!.RoleId, cancellationToken);
        var session = await _auth.RotateRefreshTokenAsync(
            account.AccountId,
            request.RefreshToken,
            account.Email,
            account.FullName,
            RoleList(role),
            cancellationToken);

        return session is null
            ? ServiceResult<AuthResponseDto>.Unauthorized("Refresh token is invalid or expired.")
            : ServiceResult<AuthResponseDto>.Success(session, "Access token refreshed successfully.");
    }

    public async Task<ServiceResult> ForgotPasswordAsync(
        ForgotPasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ServiceResult.BadRequest("Email is required.");
        }

        var email = NormalizeEmail(request.Email);
        if (!await AllowEmailAttemptAsync("forgot-password", email, 3, cancellationToken))
        {
            return ServiceResult.TooManyRequests("Too many password reset requests. Please try again later.");
        }

        var account = await _accounts.GetByEmailAsync(email, cancellationToken);
        if (account is not null && account.DeletedAt is null)
        {
            var token = await _passwordResetStore.CreateAsync(account.AccountId, cancellationToken);
            await _email.SendPasswordResetAsync(account.Email, account.FullName, token, cancellationToken);
        }

        return ServiceResult.Success("If the account exists, a password reset email has been sent.");
    }

    public async Task<ServiceResult> ResetPasswordAsync(
        ResetPasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var passwordError = ValidatePassword(request.NewPassword);
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
        {
            return ServiceResult.BadRequest("Email and reset token are required.");
        }

        if (passwordError is not null)
        {
            return ServiceResult.BadRequest(passwordError);
        }

        var email = NormalizeEmail(request.Email);
        if (!await AllowEmailAttemptAsync("reset-password", email, 5, cancellationToken))
        {
            return ServiceResult.TooManyRequests("Too many password reset attempts. Please try again later.");
        }

        var account = await _accounts.GetByEmailAsync(email, cancellationToken);
        if (account is null ||
            account.DeletedAt is not null ||
            !await _passwordResetStore.ConsumeAsync(account.AccountId, request.Token, cancellationToken))
        {
            return ServiceResult.BadRequest("Reset token is invalid or expired.");
        }

        account.PasswordHash = _passwordHasher.HashPassword(account, request.NewPassword);
        await _accounts.SaveChangesAsync(cancellationToken);
        await _refreshTokens.RevokeAllAsync(account.AccountId, cancellationToken);
        await _auth.RevokeUserAccessTokensAsync(account.AccountId, cancellationToken);
        return ServiceResult.Success("Password reset successfully.");
    }

    public async Task<ServiceResult<CurrentUserDto>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.DeletedAt is not null)
        {
            return ServiceResult<CurrentUserDto>.NotFound("Account not found.");
        }

        return ServiceResult<CurrentUserDto>.Success(await ToCurrentUserAsync(account, cancellationToken));
    }

    public async Task<ServiceResult<CurrentUserDto>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 100)
        {
            return ServiceResult<CurrentUserDto>.BadRequest("Full name is required and must not exceed 100 characters.");
        }

        if (request.Phone?.Trim().Length > 20)
        {
            return ServiceResult<CurrentUserDto>.BadRequest("Phone must not exceed 20 characters.");
        }

        var account = await _accounts.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.DeletedAt is not null)
        {
            return ServiceResult<CurrentUserDto>.NotFound("Account not found.");
        }

        account.FullName = request.FullName.Trim();
        account.Phone = NormalizeOptional(request.Phone);
        await _accounts.SaveChangesAsync(cancellationToken);
        return ServiceResult<CurrentUserDto>.Success(
            await ToCurrentUserAsync(account, cancellationToken),
            "Profile updated successfully.");
    }

    public async Task<ServiceResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var passwordError = ValidatePassword(request.NewPassword);
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return ServiceResult.BadRequest("Current password is required.");
        }

        if (passwordError is not null)
        {
            return ServiceResult.BadRequest(passwordError);
        }

        var account = await _accounts.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.DeletedAt is not null)
        {
            return ServiceResult.NotFound("Account not found.");
        }

        if (_passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.CurrentPassword) ==
            PasswordVerificationResult.Failed)
        {
            return ServiceResult.Unauthorized("Current password is invalid.");
        }

        account.PasswordHash = _passwordHasher.HashPassword(account, request.NewPassword);
        await _accounts.SaveChangesAsync(cancellationToken);
        await _refreshTokens.RevokeAllAsync(account.AccountId, cancellationToken);
        await _auth.RevokeUserAccessTokensAsync(account.AccountId, cancellationToken);
        return ServiceResult.Success("Password changed successfully.");
    }

    private async Task<CurrentUserDto> ToCurrentUserAsync(Account account, CancellationToken cancellationToken)
    {
        return new CurrentUserDto
        {
            AccountId = account.AccountId,
            Email = account.Email,
            FullName = account.FullName,
            Phone = account.Phone,
            AvatarUrl = account.AvatarUrl,
            Status = account.Status?.ToString() ?? string.Empty,
            Role = await _accounts.GetRoleNameAsync(account.RoleId, cancellationToken) ?? string.Empty
        };
    }

    private static bool CanAuthenticate(Account? account)
    {
        return account is not null && account.DeletedAt is null && account.Status == AccountStatus.ACTIVE;
    }

    private static IEnumerable<string> RoleList(string? role)
    {
        return string.IsNullOrWhiteSpace(role) ? [] : [role];
    }

    private async Task SendVerificationOtpAsync(Account account, CancellationToken cancellationToken)
    {
        var otpCode = await _emailOtpStore.CreateAsync(account.Email, cancellationToken);
        await _email.SendEmailVerificationOtpAsync(account.Email, account.FullName, otpCode, cancellationToken);
    }

    private async Task<bool> AllowEmailAttemptAsync(
        string purpose,
        string email,
        long maxAttempts,
        CancellationToken cancellationToken)
    {
        var key = $"furnispace:auth:rate-limit:{purpose}:{Sha256(email)}";
        var attempts = await _cache.IncrementAsync(key, EmailRateLimitWindow, cancellationToken);
        return attempts <= maxAttempts;
    }

    private static List<string> ValidateRegistration(RegisterRequestDto request)
    {
        var errors = new List<string>();
        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length == 0 || email.Length > 100 || !email.Contains('@', StringComparison.Ordinal))
        {
            errors.Add("Email is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 100)
        {
            errors.Add("Full name is required and must not exceed 100 characters.");
        }

        if (request.Phone?.Trim().Length > 20)
        {
            errors.Add("Phone must not exceed 20 characters.");
        }

        var passwordError = ValidatePassword(request.Password);
        if (passwordError is not null)
        {
            errors.Add(passwordError);
        }

        return errors;
    }

    private static string? ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 128)
        {
            return "Password must be between 8 and 128 characters.";
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
        {
            return "Password must contain uppercase, lowercase, and numeric characters.";
        }

        return null;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
