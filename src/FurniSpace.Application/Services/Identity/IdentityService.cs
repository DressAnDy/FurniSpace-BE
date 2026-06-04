using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs;
using FurniSpace.Application.DTOs.Identity;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.AspNetCore.Identity;

namespace FurniSpace.Application.Services.Identity;

public sealed class IdentityService : IIdentityService
{
    private const string CustomerRole = "CUSTOMER";
    private readonly IAccountRepository _accounts;
    private readonly IAuthService _auth;
    private readonly IPasswordHasher<Account> _passwordHasher;
    private readonly IPasswordResetStore _passwordResetStore;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IEmailService _email;

    public IdentityService(
        IAccountRepository accounts,
        IAuthService auth,
        IPasswordHasher<Account> passwordHasher,
        IPasswordResetStore passwordResetStore,
        IRefreshTokenStore refreshTokens,
        IEmailService email)
    {
        _accounts = accounts;
        _auth = auth;
        _passwordHasher = passwordHasher;
        _passwordResetStore = passwordResetStore;
        _refreshTokens = refreshTokens;
        _email = email;
    }

    public async Task<ServiceResult<AuthResponseDto>> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateRegistration(request);
        if (errors.Count > 0)
        {
            return ServiceResult<AuthResponseDto>.BadRequest(errors);
        }

        var email = NormalizeEmail(request.Email);
        if (await _accounts.EmailExistsAsync(email, cancellationToken: cancellationToken))
        {
            return ServiceResult<AuthResponseDto>.Conflict("Email already exists.");
        }

        var customerRoleId = await _accounts.GetRoleIdByNameAsync(CustomerRole, cancellationToken);
        if (!customerRoleId.HasValue)
        {
            return ServiceResult<AuthResponseDto>.InternalServerError("Customer role is not configured.");
        }

        var account = new Account
        {
            AccountId = Guid.NewGuid(),
            RoleId = customerRoleId.Value,
            Email = email,
            FullName = request.FullName.Trim(),
            Phone = NormalizeOptional(request.Phone),
            Status = AccountStatus.ACTIVE
        };
        account.PasswordHash = _passwordHasher.HashPassword(account, request.Password);

        await _accounts.AddAsync(account, cancellationToken);
        await _accounts.SaveChangesAsync(cancellationToken);

        var session = await _auth.CreateSessionAsync(
            account.AccountId,
            account.Email,
            account.FullName,
            [CustomerRole],
            cancellationToken);
        return ServiceResult<AuthResponseDto>.Created(session, "Account registered successfully.");
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Email and password are required.");
        }

        var account = await _accounts.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
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

        var account = await _accounts.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
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

        var account = await _accounts.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        if (account is null ||
            account.DeletedAt is not null ||
            !await _passwordResetStore.ConsumeAsync(account.AccountId, request.Token, cancellationToken))
        {
            return ServiceResult.BadRequest("Reset token is invalid or expired.");
        }

        account.PasswordHash = _passwordHasher.HashPassword(account, request.NewPassword);
        await _accounts.SaveChangesAsync(cancellationToken);
        await _refreshTokens.RevokeAllAsync(account.AccountId, cancellationToken);
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
}
