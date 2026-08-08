#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Identity;
using FurniSpace.Application.DTOs.Auth;
using FurniSpace.Application.DTOs.Identity;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.Application.Services.Identity;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace FurniSpace.Application.Tests.Identity;

public sealed class IdentityServiceTests
{
    private const string ValidPassword = "Password1";

    [Fact]
    public async Task RegisterAsync_WhenEmailDeliveryFails_ReturnsCreatedWithInactiveAccount()
    {
        var accounts = new FakeIdentityAccountRepository { RoleId = Guid.NewGuid() };
        var email = new FakeEmailService
        {
            VerificationException = new EmailDeliveryException("Provider unavailable")
        };
        var service = CreateService(accounts: accounts, email: email);

        var result = await service.RegisterAsync(new RegisterRequestDto
        {
            Email = "new-user@furnispace.com",
            FullName = "New User",
            Password = ValidPassword
        });

        Assert.Equal(201, result.Status);
        Assert.Contains("could not be sent", result.Message, StringComparison.Ordinal);
        Assert.Equal("failed", result.Data?.GetType().GetProperty("EmailDeliveryStatus")?.GetValue(result.Data));
        Assert.NotNull(accounts.AddedAccount);
        Assert.Equal(AccountStatus.INACTIVE, accounts.AddedAccount.Status);
        Assert.Equal(1, email.SendVerificationCallCount);
    }

    [Fact]
    public async Task ResendVerificationOtpAsync_WhenEmailDeliveryFails_ReturnsNeutralResponse()
    {
        var account = CreateInactiveAccount();
        var email = new FakeEmailService
        {
            VerificationException = new EmailDeliveryException("Provider unavailable")
        };
        var service = CreateService(
            accounts: new FakeIdentityAccountRepository { AccountByEmail = account },
            email: email);

        var result = await service.ResendVerificationOtpAsync(
            new ResendVerificationOtpRequestDto { Email = account.Email });

        Assert.Equal(200, result.Status);
        Assert.Equal("If the account requires verification, an OTP has been sent.", result.Message);
        Assert.Equal(1, email.SendVerificationCallCount);
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenOtpConsumeFails_ReturnsBadRequest()
    {
        var account = CreateInactiveAccount();
        var emailOtpStore = new FakeEmailOtpStore { ConsumeResult = false };
        var service = CreateService(
            accounts: new FakeIdentityAccountRepository { AccountByEmail = account },
            emailOtpStore: emailOtpStore);

        var result = await service.VerifyEmailAsync(new VerifyEmailRequestDto
        {
            Email = account.Email,
            OtpCode = "123456"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("OTP code is invalid or expired.", result.Message);
        Assert.Equal(1, emailOtpStore.ConsumeCallCount);
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenOtpValid_ActivatesAccountAndCreatesSession()
    {
        var account = CreateInactiveAccount();
        var emailOtpStore = new FakeEmailOtpStore { ConsumeResult = true };
        var auth = new FakeIdentityAuthService();
        var service = CreateService(
            accounts: new FakeIdentityAccountRepository
            {
                AccountByEmail = account,
                RoleName = "CUSTOMER"
            },
            auth: auth,
            emailOtpStore: emailOtpStore);

        var result = await service.VerifyEmailAsync(new VerifyEmailRequestDto
        {
            Email = account.Email,
            OtpCode = "123456"
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Email verified successfully.", result.Message);
        Assert.Equal(AccountStatus.ACTIVE, account.Status);
        Assert.Equal(1, emailOtpStore.ConsumeCallCount);
        Assert.Equal(1, auth.CreateSessionCallCount);
    }

    [Fact]
    public async Task RefreshAsync_WhenRefreshTokenUnknown_ReturnsUnauthorized()
    {
        var refreshTokens = new FakeRefreshTokenStore { ResolvedUserId = null };
        var service = CreateService(refreshTokens: refreshTokens);

        var result = await service.RefreshAsync(new RefreshRequestDto { RefreshToken = "missing-token" });

        Assert.Equal(401, result.Status);
        Assert.Equal("Refresh token is invalid or expired.", result.Message);
        Assert.Equal(1, refreshTokens.ResolveUserIdCallCount);
    }

    [Fact]
    public async Task RefreshAsync_WhenRefreshTokenValid_ReturnsNewSession()
    {
        var userId = Guid.NewGuid();
        var account = CreateActiveAccount(userId);
        var refreshTokens = new FakeRefreshTokenStore { ResolvedUserId = userId };
        var auth = new FakeIdentityAuthService
        {
            RotateResult = new AuthResponseDto { AccessToken = "new-access-token" }
        };
        var service = CreateService(
            accounts: new FakeIdentityAccountRepository { AccountById = account, RoleName = "CUSTOMER" },
            auth: auth,
            refreshTokens: refreshTokens);

        var result = await service.RefreshAsync(new RefreshRequestDto { RefreshToken = "valid-token" });

        Assert.Equal(200, result.Status);
        Assert.Equal("Access token refreshed successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal("new-access-token", result.Data.AccessToken);
        Assert.Equal(1, refreshTokens.ResolveUserIdCallCount);
        Assert.Equal(1, auth.RotateRefreshTokenCallCount);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenAccountExists_CreatesResetTokenAndSendsEmail()
    {
        var account = CreateActiveAccount();
        var passwordResetStore = new FakePasswordResetStore { Token = "reset-token" };
        var email = new FakeEmailService();
        var service = CreateService(
            accounts: new FakeIdentityAccountRepository { AccountByEmail = account },
            passwordResetStore: passwordResetStore,
            email: email);

        var result = await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = account.Email });

        Assert.Equal(200, result.Status);
        Assert.Equal(1, passwordResetStore.CreateCallCount);
        Assert.Equal(account.AccountId, passwordResetStore.LastUserId);
        Assert.Equal(1, email.SendPasswordResetCallCount);
        Assert.Equal(account.Email, email.LastPasswordResetEmail);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenEmailDeliveryFails_ReturnsNeutralResponse()
    {
        var account = CreateActiveAccount();
        var email = new FakeEmailService
        {
            PasswordResetException = new EmailDeliveryException("Provider unavailable")
        };
        var service = CreateService(
            accounts: new FakeIdentityAccountRepository { AccountByEmail = account },
            email: email);

        var result = await service.ForgotPasswordAsync(
            new ForgotPasswordRequestDto { Email = account.Email });

        Assert.Equal(200, result.Status);
        Assert.Equal("If the account exists, a password reset email has been sent.", result.Message);
        Assert.Equal(1, email.SendPasswordResetCallCount);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenInvalid_ReturnsBadRequest()
    {
        var account = CreateActiveAccount();
        var passwordResetStore = new FakePasswordResetStore { ConsumeResult = false };
        var service = CreateService(
            accounts: new FakeIdentityAccountRepository { AccountByEmail = account },
            passwordResetStore: passwordResetStore);

        var result = await service.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = account.Email,
            Token = "invalid-token",
            NewPassword = ValidPassword
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Reset token is invalid or expired.", result.Message);
        Assert.Equal(1, passwordResetStore.ConsumeCallCount);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenValid_ResetsPasswordAndRevokesTokens()
    {
        var account = CreateActiveAccount();
        account.PasswordHash = "old-hash";
        var passwordResetStore = new FakePasswordResetStore { ConsumeResult = true };
        var refreshTokens = new FakeRefreshTokenStore();
        var auth = new FakeIdentityAuthService();
        var service = CreateService(
            accounts: new FakeIdentityAccountRepository { AccountByEmail = account },
            passwordResetStore: passwordResetStore,
            refreshTokens: refreshTokens,
            auth: auth);

        var result = await service.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = account.Email,
            Token = "valid-token",
            NewPassword = ValidPassword
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Password reset successfully.", result.Message);
        Assert.NotEqual("old-hash", account.PasswordHash);
        Assert.Equal(1, passwordResetStore.ConsumeCallCount);
        Assert.Equal(1, refreshTokens.RevokeAllCallCount);
        Assert.Equal(account.AccountId, refreshTokens.LastRevokeAllUserId);
        Assert.Equal(1, auth.RevokeUserAccessTokensCallCount);
    }

    private static IdentityService CreateService(
        FakeIdentityAccountRepository? accounts = null,
        FakeIdentityAuthService? auth = null,
        FakeEmailOtpStore? emailOtpStore = null,
        FakePasswordResetStore? passwordResetStore = null,
        FakeRefreshTokenStore? refreshTokens = null,
        FakeEmailService? email = null)
    {
        accounts ??= new FakeIdentityAccountRepository();
        auth ??= new FakeIdentityAuthService();
        emailOtpStore ??= new FakeEmailOtpStore();
        passwordResetStore ??= new FakePasswordResetStore();
        refreshTokens ??= new FakeRefreshTokenStore();
        email ??= new FakeEmailService();

        var verificationStores = new IdentityVerificationStores(
            passwordResetStore,
            emailOtpStore,
            refreshTokens);

        return new IdentityService(
            accounts,
            auth,
            new PasswordHasher<Account>(),
            verificationStores,
            email,
            new InMemoryCacheService(),
            TestUnitOfWork.ForSaveChanges(_ => Task.FromResult(1)));
    }

    private static Account CreateInactiveAccount(Guid? accountId = null)
    {
        return new Account
        {
            AccountId = accountId ?? Guid.NewGuid(),
            RoleId = Guid.NewGuid(),
            Email = "inactive@furnispace.com",
            PasswordHash = "hash",
            FullName = "Inactive User",
            Status = AccountStatus.INACTIVE
        };
    }

    private static Account CreateActiveAccount(Guid? accountId = null)
    {
        return new Account
        {
            AccountId = accountId ?? Guid.NewGuid(),
            RoleId = Guid.NewGuid(),
            Email = "active@furnispace.com",
            PasswordHash = "hash",
            FullName = "Active User",
            Status = AccountStatus.ACTIVE
        };
    }

    private sealed class FakeIdentityAccountRepository : IAccountRepository
    {
        public Account? AccountByEmail { get; set; }
        public Account? AccountById { get; set; }
        public Account? AddedAccount { get; private set; }
        public Guid? RoleId { get; set; }
        public string? RoleName { get; set; }

        public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AccountByEmail?.Email.Equals(email, StringComparison.OrdinalIgnoreCase) == true
                ? AccountByEmail
                : null);
        }

        public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AccountById?.AccountId == id ? AccountById : null);
        }

        public Task<string?> GetRoleNameAsync(Guid roleId, CancellationToken cancellationToken = default)
        {
            _ = roleId;
            return Task.FromResult(RoleName);
        }

        public IQueryable<Account> Query() => Array.Empty<Account>().AsQueryable();
        public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Account>>([]);
        public Task AddAsync(Account entity, CancellationToken cancellationToken = default)
        {
            AddedAccount = entity;
            return Task.CompletedTask;
        }
        public Task AddRangeAsync(IEnumerable<Account> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Account entity) { }
        public void Remove(Account entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<Infrastructure.ReadModels.Accounts.AccountDetailReadModel?> GetDetailAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<Infrastructure.ReadModels.Accounts.AccountDetailReadModel?>(null);
        public Task<Guid?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default) => Task.FromResult(RoleId);
        public Task<bool> RoleExistsAsync(Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> EmailExistsAsync(string email, Guid? excludedAccountId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Infrastructure.ReadModels.Accounts.AvailableDesignerReadModel>> GetAvailableDesignersAsync(int page, int pageSize, int maxActiveProjects, string? search, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Accounts.AvailableDesignerReadModel>>([]);
        public Task<int> CountAvailableDesignersAsync(int maxActiveProjects, string? search, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Infrastructure.ReadModels.Accounts.AvailableDesignerReadModel>> GetDesignerWorkloadAsync(int page, int pageSize, int maxActiveProjects, string? search, string? capacityState, string sortBy, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Accounts.AvailableDesignerReadModel>>([]);
        public Task<int> CountDesignerWorkloadAsync(int maxActiveProjects, string? search, string? capacityState, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<Infrastructure.ReadModels.Accounts.DesignerWorkloadSummaryReadModel> GetDesignerWorkloadSummaryAsync(int maxActiveProjects, CancellationToken cancellationToken = default) => Task.FromResult(new Infrastructure.ReadModels.Accounts.DesignerWorkloadSummaryReadModel());
        public Task<IReadOnlyList<Infrastructure.ReadModels.Accounts.DesignerAssignedProjectReadModel>> GetDesignerAssignedProjectsAsync(Guid designerId, int page, int pageSize, string? bucket, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Accounts.DesignerAssignedProjectReadModel>>([]);
        public Task<int> CountDesignerAssignedProjectsAsync(Guid designerId, string? bucket, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> IsActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Infrastructure.ReadModels.Accounts.SalesWorkloadItemReadModel>> GetSalesWorkloadAsync(int page, int pageSize, int maxActiveProjects, string? search, string? capacityState, string? futurePressureState, string sortBy, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Accounts.SalesWorkloadItemReadModel>>([]);
        public Task<int> CountSalesWorkloadAsync(int maxActiveProjects, string? search, string? capacityState, string? futurePressureState, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<Infrastructure.ReadModels.Accounts.SalesWorkloadSummaryReadModel> GetSalesWorkloadSummaryAsync(int maxActiveProjects, CancellationToken cancellationToken = default) => Task.FromResult(new Infrastructure.ReadModels.Accounts.SalesWorkloadSummaryReadModel());
        public Task<IReadOnlyList<Infrastructure.ReadModels.Accounts.SalesAssignedProjectReadModel>> GetSalesAssignedProjectsAsync(Guid salesId, int page, int pageSize, string? bucket, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Accounts.SalesAssignedProjectReadModel>>([]);
        public Task<int> CountSalesAssignedProjectsAsync(Guid salesId, string? bucket, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Infrastructure.ReadModels.Accounts.UnassignedIntakeProjectReadModel>> GetUnassignedIntakeProjectsAsync(int page, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Accounts.UnassignedIntakeProjectReadModel>>([]);
        public Task<int> CountUnassignedIntakeProjectsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> IsActiveSalesAsync(Guid salesId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Account>> GetPagedAsync(int page, int pageSize, string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Account>>([]);
        public Task<int> CountAsync(string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Infrastructure.ReadModels.Accounts.AccountFacetCountReadModel>> CountGroupedByStatusAsync(bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Accounts.AccountFacetCountReadModel>>([]);
        public Task<IReadOnlyList<Infrastructure.ReadModels.Accounts.AccountFacetCountReadModel>> CountGroupedByRoleIdAsync(bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Accounts.AccountFacetCountReadModel>>([]);
    }

    private sealed class FakeIdentityAuthService : IAuthService
    {
        public AuthResponseDto? RotateResult { get; set; }
        public int CreateSessionCallCount { get; private set; }
        public int RotateRefreshTokenCallCount { get; private set; }
        public int RevokeUserAccessTokensCallCount { get; private set; }

        public Task<AuthResponseDto> CreateSessionAsync(Guid userId, string email, string fullName, IEnumerable<string>? roles = null, CancellationToken cancellationToken = default)
        {
            CreateSessionCallCount++;
            return Task.FromResult(new AuthResponseDto { AccessToken = "access-token" });
        }

        public Task<bool> ValidateRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<AuthResponseDto?> RotateRefreshTokenAsync(Guid userId, string refreshToken, string email, string fullName, IEnumerable<string>? roles = null, CancellationToken cancellationToken = default)
        {
            RotateRefreshTokenCallCount++;
            return Task.FromResult(RotateResult);
        }

        public Task RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeAccessTokenAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeUserAccessTokensAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            RevokeUserAccessTokensCallCount++;
            return Task.CompletedTask;
        }

        public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsAccessTokenRevokedAsync(string jti, Guid userId, DateTimeOffset issuedAt, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeEmailOtpStore : IEmailOtpStore
    {
        public bool ConsumeResult { get; set; } = true;
        public int ConsumeCallCount { get; private set; }

        public Task<string> CreateAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult("123456");

        public Task<bool> ConsumeAsync(string email, string otpCode, CancellationToken cancellationToken = default)
        {
            ConsumeCallCount++;
            return Task.FromResult(ConsumeResult);
        }
    }

    private sealed class FakePasswordResetStore : IPasswordResetStore
    {
        public string Token { get; set; } = "reset-token";
        public bool ConsumeResult { get; set; } = true;
        public int CreateCallCount { get; private set; }
        public int ConsumeCallCount { get; private set; }
        public Guid LastUserId { get; private set; }

        public Task<string> CreateAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            LastUserId = userId;
            return Task.FromResult(Token);
        }

        public Task<bool> ConsumeAsync(Guid userId, string token, CancellationToken cancellationToken = default)
        {
            ConsumeCallCount++;
            LastUserId = userId;
            return Task.FromResult(ConsumeResult);
        }
    }

    private sealed class FakeRefreshTokenStore : IRefreshTokenStore
    {
        public Guid? ResolvedUserId { get; set; }
        public int ResolveUserIdCallCount { get; private set; }
        public int RevokeAllCallCount { get; private set; }
        public Guid LastRevokeAllUserId { get; private set; }

        public Task StoreAsync(Guid userId, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Guid?> ResolveUserIdAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            ResolveUserIdCallCount++;
            return Task.FromResult(ResolvedUserId);
        }

        public Task<bool> ConsumeAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RevokeAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            RevokeAllCallCount++;
            LastRevokeAllUserId = userId;
            return Task.CompletedTask;
        }

        public Task RevokeAccessTokenAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeUserAccessTokensAsync(Guid userId, DateTimeOffset revokedAt, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> AreUserAccessTokensRevokedAsync(Guid userId, DateTimeOffset issuedAt, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeEmailService : IEmailService
    {
        public Exception? PasswordResetException { get; set; }
        public Exception? VerificationException { get; set; }
        public int SendPasswordResetCallCount { get; private set; }
        public int SendVerificationCallCount { get; private set; }
        public string? LastPasswordResetEmail { get; private set; }

        public Task SendPasswordResetAsync(string recipientEmail, string recipientName, string resetToken, CancellationToken cancellationToken = default)
        {
            SendPasswordResetCallCount++;
            LastPasswordResetEmail = recipientEmail;
            return PasswordResetException is null
                ? Task.CompletedTask
                : Task.FromException(PasswordResetException);
        }

        public Task SendEmailVerificationOtpAsync(string recipientEmail, string recipientName, string otpCode, CancellationToken cancellationToken = default)
        {
            SendVerificationCallCount++;
            return VerificationException is null
                ? Task.CompletedTask
                : Task.FromException(VerificationException);
        }
    }
}
