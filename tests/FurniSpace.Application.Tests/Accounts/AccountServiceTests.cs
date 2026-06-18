#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.DTOs.Auth;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.Application.Mappings;
using FurniSpace.Application.Services.Accounts;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Accounts;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using Xunit;

namespace FurniSpace.Application.Tests.Accounts;

public sealed class AccountServiceTests
{
    static AccountServiceTests()
    {
        new AccountMappingConfig().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public async Task GetAdminDetailAsync_WithExistingAccount_ReturnsRoleAndProfileWithoutPassword()
    {
        var accountId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var repository = new FakeAccountRepository
        {
            Detail = new AccountDetailReadModel
            {
                AccountId = accountId,
                Email = "designer01@furnispace.com",
                FullName = "Emily Davis",
                Phone = "0900000002",
                AvatarUrl = null,
                Role = new AccountRoleReadModel
                {
                    RoleId = roleId,
                    RoleName = "DESIGNER",
                    Description = "Designer Staff"
                },
                Status = AccountStatus.ACTIVE,
                CreatedAt = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc)
            }
        };
        var service = CreateService(repository);

        var result = await service.GetAdminDetailAsync(accountId);

        Assert.Equal(200, result.Status);
        Assert.Equal("Account detail retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(accountId, result.Data.AccountId);
        Assert.Equal("designer01@furnispace.com", result.Data.Email);
        Assert.Equal("Emily Davis", result.Data.FullName);
        Assert.Equal("0900000002", result.Data.Phone);
        Assert.Null(result.Data.AvatarUrl);
        Assert.Equal(roleId, result.Data.Role.RoleId);
        Assert.Equal("DESIGNER", result.Data.Role.RoleName);
        Assert.Equal("Designer Staff", result.Data.Role.Description);
        Assert.Equal("ACTIVE", result.Data.Status);
        Assert.Null(result.Data.DeletedAt);
        Assert.Equal(1, repository.GetDetailCallCount);
        Assert.Equal(accountId, repository.DetailAccountId);
        Assert.DoesNotContain(
            typeof(AccountDetailDto).GetProperties(),
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetAdminDetailAsync_WithDeletedAccount_ReturnsDetailForAdminReview()
    {
        var deletedAt = new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc);
        var repository = new FakeAccountRepository
        {
            Detail = new AccountDetailReadModel
            {
                AccountId = Guid.NewGuid(),
                Email = "deleted@furnispace.com",
                FullName = "Deleted User",
                Role = new AccountRoleReadModel
                {
                    RoleId = Guid.NewGuid(),
                    RoleName = "CUSTOMER"
                },
                Status = AccountStatus.INACTIVE,
                DeletedAt = deletedAt
            }
        };
        var service = CreateService(repository);

        var result = await service.GetAdminDetailAsync(repository.Detail.AccountId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(deletedAt, result.Data.DeletedAt);
        Assert.Equal("INACTIVE", result.Data.Status);
    }

    [Fact]
    public async Task GetAdminDetailAsync_WithEmptyAccountId_ReturnsBadRequest()
    {
        var repository = new FakeAccountRepository();
        var service = CreateService(repository);

        var result = await service.GetAdminDetailAsync(Guid.Empty);

        Assert.Equal(400, result.Status);
        Assert.Equal("Account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetDetailCallCount);
    }

    [Fact]
    public async Task GetAdminDetailAsync_WithMissingAccount_ReturnsAccountNotFoundCode()
    {
        var repository = new FakeAccountRepository();
        var service = CreateService(repository);

        var result = await service.GetAdminDetailAsync(Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("ACCOUNT_NOT_FOUND", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetDetailCallCount);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WithValidRequest_UpdatesOnlyBasicProfile()
    {
        var accountId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var account = new Account
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = "sales01@furnispace.com",
            PasswordHash = "hashed-password",
            FullName = "Old Name",
            Phone = "0999999999",
            AvatarUrl = "https://cdn.furnispace.com/avatars/sarah.png",
            Status = AccountStatus.ACTIVE,
            UpdatedAt = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc)
        };
        var repository = new FakeAccountRepository
        {
            Accounts = [account],
            RoleNames = { [roleId] = "SALES" }
        };
        var service = CreateService(repository);

        var result = await service.UpdateMyProfileAsync(accountId, new UpdateMyProfileRequestDto
        {
            FullName = " Sarah Johnson ",
            Phone = " 0900000001 "
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Profile updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(accountId, result.Data.AccountId);
        Assert.Equal("sales01@furnispace.com", result.Data.Email);
        Assert.Equal("Sarah Johnson", result.Data.FullName);
        Assert.Equal("0900000001", result.Data.Phone);
        Assert.Equal("https://cdn.furnispace.com/avatars/sarah.png", result.Data.AvatarUrl);
        Assert.Equal("SALES", result.Data.Role);
        Assert.Equal("ACTIVE", result.Data.Status);
        Assert.NotNull(result.Data.UpdatedAt);
        Assert.True(result.Data.UpdatedAt > new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc));
        Assert.Equal("sales01@furnispace.com", account.Email);
        Assert.Equal(roleId, account.RoleId);
        Assert.Equal(AccountStatus.ACTIVE, account.Status);
        Assert.Equal("hashed-password", account.PasswordHash);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, repository.GetRoleNameCallCount);
        Assert.DoesNotContain(
            typeof(MyProfileDto).GetProperties(),
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WithBlankPhone_SetsPhoneToNull()
    {
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            AccountId = accountId,
            RoleId = Guid.NewGuid(),
            Email = "customer@furnispace.com",
            PasswordHash = "hashed-password",
            FullName = "Customer",
            Phone = "0900000001",
            Status = AccountStatus.ACTIVE
        };
        var repository = new FakeAccountRepository { Accounts = [account] };
        var service = CreateService(repository);

        var result = await service.UpdateMyProfileAsync(accountId, new UpdateMyProfileRequestDto
        {
            FullName = "Customer Updated",
            Phone = " "
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.Phone);
        Assert.Null(account.Phone);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WithEmptyCurrentUserId_ReturnsBadRequest()
    {
        var repository = new FakeAccountRepository();
        var service = CreateService(repository);

        var result = await service.UpdateMyProfileAsync(Guid.Empty, new UpdateMyProfileRequestDto
        {
            FullName = "Sarah Johnson"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WithInvalidRequest_ReturnsValidationErrors()
    {
        var repository = new FakeAccountRepository();
        var service = CreateService(repository);

        var result = await service.UpdateMyProfileAsync(Guid.NewGuid(), new UpdateMyProfileRequestDto
        {
            FullName = " ",
            Phone = new string('1', 21)
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("Full name is required.", result.Errors!);
        Assert.Contains("Phone must not exceed 20 characters.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WithTooLongFullName_ReturnsValidationError()
    {
        var repository = new FakeAccountRepository();
        var service = CreateService(repository);

        var result = await service.UpdateMyProfileAsync(Guid.NewGuid(), new UpdateMyProfileRequestDto
        {
            FullName = new string('A', 101)
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Full name must not exceed 100 characters.", result.Errors!);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WithMissingAccount_ReturnsNotFound()
    {
        var repository = new FakeAccountRepository();
        var service = CreateService(repository);

        var result = await service.UpdateMyProfileAsync(Guid.NewGuid(), new UpdateMyProfileRequestDto
        {
            FullName = "Sarah Johnson"
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("Account not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WithDeletedAccount_ReturnsNotFound()
    {
        var accountId = Guid.NewGuid();
        var repository = new FakeAccountRepository
        {
            Accounts =
            [
                new Account
                {
                    AccountId = accountId,
                    Email = "deleted@furnispace.com",
                    PasswordHash = "hashed-password",
                    FullName = "Deleted User",
                    DeletedAt = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc)
                }
            ]
        };
        var service = CreateService(repository);

        var result = await service.UpdateMyProfileAsync(accountId, new UpdateMyProfileRequestDto
        {
            FullName = "Sarah Johnson"
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("Account not found.", result.Message);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetAvailableDesignersAsync_WithValidQuery_ReturnsPagedDesigners()
    {
        var designerId = Guid.NewGuid();
        var repository = new FakeAccountRepository
        {
            AvailableDesigners =
            [
                new AvailableDesignerReadModel
                {
                    AccountId = designerId,
                    Email = "designer01@furnispace.com",
                    FullName = "Emily Davis",
                    Phone = "0900000002",
                    AvatarUrl = null,
                    Status = AccountStatus.ACTIVE,
                    CurrentActiveProjectCount = 1,
                    MaxActiveProjects = 2,
                    AvailableSlot = 1,
                    CreatedAt = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc)
                }
            ]
        };
        var service = CreateService(repository);

        var result = await service.GetAvailableDesignersAsync(new AvailableDesignerQueryDto
        {
            Page = 1,
            PageSize = 10,
            Search = " Emily "
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Available designers retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(10, result.Data.PageSize);
        Assert.Equal(1, result.Data.TotalItems);
        Assert.Equal(1, result.Data.TotalPages);
        var designer = Assert.Single(result.Data.Items);
        Assert.Equal(designerId, designer.AccountId);
        Assert.Equal("designer01@furnispace.com", designer.Email);
        Assert.Equal("Emily Davis", designer.FullName);
        Assert.Equal("0900000002", designer.Phone);
        Assert.Equal("ACTIVE", designer.Status);
        Assert.Equal(1, designer.CurrentActiveProjectCount);
        Assert.Equal(2, designer.MaxActiveProjects);
        Assert.Equal(1, designer.AvailableSlot);
        Assert.Equal(1, repository.GetAvailableDesignersCallCount);
        Assert.Equal(1, repository.CountAvailableDesignersCallCount);
        Assert.Equal(1, repository.Page);
        Assert.Equal(10, repository.PageSize);
        Assert.Equal(2, repository.MaxActiveProjects);
        Assert.Equal("Emily", repository.Search);
        Assert.DoesNotContain(
            typeof(AvailableDesignerDto).GetProperties(),
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetAvailableDesignersAsync_WithEmptyResult_ReturnsEmptyPage()
    {
        var repository = new FakeAccountRepository();
        var service = CreateService(repository);

        var result = await service.GetAvailableDesignersAsync(new AvailableDesignerQueryDto
        {
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, result.Data.TotalItems);
        Assert.Equal(0, result.Data.TotalPages);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetAvailableDesignersAsync_WithInvalidPage_ReturnsBadRequest(int page)
    {
        var repository = new FakeAccountRepository();
        var service = CreateService(repository);

        var result = await service.GetAvailableDesignersAsync(new AvailableDesignerQueryDto
        {
            Page = page,
            PageSize = 10
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Page must be greater than zero.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAvailableDesignersCallCount);
        Assert.Equal(0, repository.CountAvailableDesignersCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task GetAvailableDesignersAsync_WithInvalidPageSize_ReturnsBadRequest(int pageSize)
    {
        var repository = new FakeAccountRepository();
        var service = CreateService(repository);

        var result = await service.GetAvailableDesignersAsync(new AvailableDesignerQueryDto
        {
            Page = 1,
            PageSize = pageSize
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Page size must be between 1 and 100.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAvailableDesignersCallCount);
        Assert.Equal(0, repository.CountAvailableDesignersCallCount);
    }

    private static AccountService CreateService(FakeAccountRepository repository)
    {
        return new AccountService(
            repository,
            new FakeAuthService(),
            new InMemoryCacheService(),
            new FakeSearchIndexService(),
            TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        public AccountDetailReadModel? Detail { get; set; }
        public IReadOnlyList<Account> Accounts { get; set; } = [];
        public IReadOnlyList<AvailableDesignerReadModel> AvailableDesigners { get; set; } = [];
        public Dictionary<Guid, string> RoleNames { get; } = [];
        public int GetDetailCallCount { get; private set; }
        public Guid DetailAccountId { get; private set; }
        public int GetByIdCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public int GetRoleNameCallCount { get; private set; }
        public int GetAvailableDesignersCallCount { get; private set; }
        public int CountAvailableDesignersCallCount { get; private set; }
        public int Page { get; private set; }
        public int PageSize { get; private set; }
        public int MaxActiveProjects { get; private set; }
        public string? Search { get; private set; }

        public Task<AccountDetailReadModel?> GetDetailAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            GetDetailCallCount++;
            DetailAccountId = accountId;
            return Task.FromResult(Detail?.AccountId == accountId ? Detail : null);
        }

        public IQueryable<Account> Query() => Enumerable.Empty<Account>().AsQueryable();
        public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            return Task.FromResult(Accounts.FirstOrDefault(account => account.AccountId == id));
        }

        public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Account>>([]);
        public Task AddAsync(Account entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Account> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Account entity) { }
        public void Remove(Account entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }

        public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult<Account?>(null);
        public Task<string?> GetRoleNameAsync(Guid roleId, CancellationToken cancellationToken = default)
        {
            GetRoleNameCallCount++;
            return Task.FromResult(RoleNames.GetValueOrDefault(roleId));
        }

        public Task<Guid?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public Task<bool> RoleExistsAsync(Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> EmailExistsAsync(string email, Guid? excludedAccountId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<AvailableDesignerReadModel>> GetAvailableDesignersAsync(
            int page,
            int pageSize,
            int maxActiveProjects,
            string? search,
            CancellationToken cancellationToken = default)
        {
            GetAvailableDesignersCallCount++;
            Page = page;
            PageSize = pageSize;
            MaxActiveProjects = maxActiveProjects;
            Search = search;
            return Task.FromResult<IReadOnlyList<AvailableDesignerReadModel>>(
                AvailableDesigners
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList());
        }

        public Task<int> CountAvailableDesignersAsync(
            int maxActiveProjects,
            string? search,
            CancellationToken cancellationToken = default)
        {
            CountAvailableDesignersCallCount++;
            return Task.FromResult(AvailableDesigners.Count);
        }

        public Task<IReadOnlyList<Account>> GetPagedAsync(int page, int pageSize, string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Account>>([]);
        public Task<int> CountAsync(string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeAuthService : IAuthService
    {
        public Task<AuthResponseDto> CreateSessionAsync(Guid userId, string email, string fullName, IEnumerable<string>? roles = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AuthResponseDto());
        }

        public Task<bool> ValidateRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<AuthResponseDto?> RotateRefreshTokenAsync(Guid userId, string refreshToken, string email, string fullName, IEnumerable<string>? roles = null, CancellationToken cancellationToken = default) => Task.FromResult<AuthResponseDto?>(null);
        public Task RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeAccessTokenAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeUserAccessTokensAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsAccessTokenRevokedAsync(string jti, Guid userId, DateTimeOffset issuedAt, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeSearchIndexService : ISearchIndexService
    {
        public Task IndexAsync<TDocument>(string indexName, string id, TDocument document, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string indexName, string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(string indexName, string query, int size = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TDocument>>([]);
        }
    }
}
