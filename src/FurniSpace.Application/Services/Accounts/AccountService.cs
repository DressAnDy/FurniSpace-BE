using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.DTOs.Search;
using FurniSpace.Application.Interfaces.Accounts;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.Application.Services.Search;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Persistence;
using Mapster;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using InfrastructureCacheService = FurniSpace.Infrastructure.Interfaces.ICacheService;
using InfrastructureSearchIndexService = FurniSpace.Infrastructure.Interfaces.ISearchIndexService;

namespace FurniSpace.Application.Services.Accounts;

public sealed class AccountService : IAccountService
{
    private const string AccountIndexName = "accounts";
    private const string AccountItemCachePrefix = "furnispace:accounts:item:";
    private const string AccountListCachePrefix = "furnispace:accounts:list:";
    private const string AccountNotFoundCode = "ACCOUNT_NOT_FOUND";
    private const string AccountDetailRetrievedMessage = "Account detail retrieved successfully.";
    private const string ProfileUpdatedMessage = "Profile updated successfully.";
    private const string AvailableDesignersRetrievedMessage = "Available designers retrieved successfully.";
    private const int MaxActiveDesignerProjects = 2;
    private const long PerfLogThresholdMs = 200;
    private static readonly TimeSpan AccountItemCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AccountListCacheTtl = TimeSpan.FromMinutes(5);

    private readonly IAccountRepository _accounts;
    private readonly IAuthService _auth;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InfrastructureCacheService _cache;
    private readonly InfrastructureSearchIndexService _search;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        IAccountRepository accounts,
        IAuthService auth,
        InfrastructureCacheService cache,
        InfrastructureSearchIndexService search,
        IUnitOfWork unitOfWork,
        ILogger<AccountService> logger)
    {
        _accounts = accounts;
        _auth = auth;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _search = search;
        _logger = logger;
    }

    public async Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationErrors = ValidateCreateRequest(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<AccountDto>.BadRequest(validationErrors);
        }

        var email = NormalizeEmail(request.Email);
        if (!await _accounts.RoleExistsAsync(request.RoleId, cancellationToken))
        {
            return ServiceResult<AccountDto>.BadRequest("Role does not exist.");
        }

        if (await _accounts.EmailExistsAsync(email, cancellationToken: cancellationToken))
        {
            return ServiceResult<AccountDto>.Conflict("Email already exists.");
        }

        var account = new Account
        {
            AccountId = Guid.NewGuid(),
            RoleId = request.RoleId,
            Email = email,
            PasswordHash = request.PasswordHash.Trim(),
            FullName = request.FullName.Trim(),
            Phone = NormalizeOptional(request.Phone),
            AvatarUrl = NormalizeOptional(request.AvatarUrl),
            Status = NormalizeStatus(request.Status)
        };

        await _accounts.AddAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = account.Adapt<AccountDto>();
        await CacheAccountAsync(dto, cancellationToken);
        await InvalidateAccountListsAsync(cancellationToken);
        await IndexAccountAsync(dto, cancellationToken);

        return ServiceResult<AccountDto>.Created(dto);
    }

    public async Task<ServiceResult<AccountDto>> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var cacheKey = AccountItemCacheKey(accountId);
        var cached = await TryGetCacheAsync<AccountDto>(cacheKey, cancellationToken);
        if (cached is not null && cached.DeletedAt is null)
        {
            return ServiceResult<AccountDto>.Success(cached);
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null || account.DeletedAt is not null)
        {
            return ServiceResult<AccountDto>.NotFound("Account not found.");
        }

        var dto = account.Adapt<AccountDto>();
        await CacheAccountAsync(dto, cancellationToken);

        return ServiceResult<AccountDto>.Success(dto);
    }

    public async Task<ServiceResult<AccountDetailDto>> GetAdminDetailAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty)
        {
            return ServiceResult<AccountDetailDto>.BadRequest("Account id is required.");
        }

        var account = await _accounts.GetDetailAsync(accountId, cancellationToken);
        return account is null
            ? ServiceResult<AccountDetailDto>.NotFound(AccountNotFoundCode)
            : ServiceResult<AccountDetailDto>.Success(account.Adapt<AccountDetailDto>(), AccountDetailRetrievedMessage);
    }

    public async Task<ServiceResult<MyProfileDto>> UpdateMyProfileAsync(
        Guid currentUserId,
        UpdateMyProfileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<MyProfileDto>.BadRequest("Account id is required.");
        }

        var validationErrors = ValidateProfileUpdateRequest(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<MyProfileDto>.BadRequest(validationErrors);
        }

        var account = await _accounts.GetByIdAsync(currentUserId, cancellationToken);
        if (account is null || account.DeletedAt is not null)
        {
            return ServiceResult<MyProfileDto>.NotFound("Account not found.");
        }

        account.FullName = request.FullName.Trim();
        account.Phone = NormalizeOptional(request.Phone);
        account.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accountDto = account.Adapt<AccountDto>();
        await CacheAccountAsync(accountDto, cancellationToken);
        await InvalidateAccountListsAsync(cancellationToken);
        await IndexAccountAsync(accountDto, cancellationToken);

        var dto = account.Adapt<MyProfileDto>();
        dto.Role = await _accounts.GetRoleNameAsync(account.RoleId, cancellationToken) ?? string.Empty;
        return ServiceResult<MyProfileDto>.Success(dto, ProfileUpdatedMessage);
    }

    public async Task<ServiceResult<PagedResult<AvailableDesignerDto>>> GetAvailableDesignersAsync(
        AvailableDesignerQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
        {
            return ServiceResult<PagedResult<AvailableDesignerDto>>.BadRequest("Page must be greater than zero.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            return ServiceResult<PagedResult<AvailableDesignerDto>>.BadRequest("Page size must be between 1 and 100.");
        }

        var normalizedSearch = NormalizeOptional(query.Search);
        var designers = await _accounts.GetAvailableDesignersAsync(
            query.Page,
            query.PageSize,
            MaxActiveDesignerProjects,
            normalizedSearch,
            cancellationToken);
        var totalItems = await _accounts.CountAvailableDesignersAsync(
            MaxActiveDesignerProjects,
            normalizedSearch,
            cancellationToken);
        var data = PagedResult<AvailableDesignerDto>.Create(
            designers.Adapt<List<AvailableDesignerDto>>(),
            query.Page,
            query.PageSize,
            totalItems);

        return ServiceResult<PagedResult<AvailableDesignerDto>>.Success(data, AvailableDesignersRetrievedMessage);
    }

    public async Task<ServiceResult<PagedResult<AccountDto>>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return ServiceResult<PagedResult<AccountDto>>.BadRequest("Page must be greater than zero.");
        }

        if (pageSize is < 1 or > 100)
        {
            return ServiceResult<PagedResult<AccountDto>>.BadRequest("Page size must be between 1 and 100.");
        }

        if (!TryNormalizeStatus(status, out var normalizedStatus))
        {
            return ServiceResult<PagedResult<AccountDto>>.BadRequest("Status is invalid.");
        }

        var normalizedSearch = NormalizeOptional(search);
        var cacheKey = AccountListCacheKey(page, pageSize, normalizedSearch, normalizedStatus, includeDeleted);

        var swTotal = Stopwatch.StartNew();

        var swRedisGet = Stopwatch.StartNew();
        var cached = await TryGetCacheAsync<PagedResult<AccountDto>>(cacheKey, cancellationToken);
        swRedisGet.Stop();

        if (cached is not null)
        {
            swTotal.Stop();
            if (swTotal.ElapsedMilliseconds >= PerfLogThresholdMs)
            {
                _logger.LogWarning(
                    "[PERF] GetPagedAccounts cache=HIT redisGetMs={RedisGetMs} totalMs={TotalMs}",
                    swRedisGet.Elapsed.TotalMilliseconds,
                    swTotal.Elapsed.TotalMilliseconds);
            }

            return ServiceResult<PagedResult<AccountDto>>.Success(cached);
        }

        long dbListMs = 0, dbCountMs = 0, redisSetMs = 0;
        PagedResult<AccountDto> result;

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var swDb = Stopwatch.StartNew();
            result = await SearchAccountsAsync(page, pageSize, normalizedSearch, normalizedStatus, includeDeleted, cancellationToken);
            swDb.Stop();
            dbListMs = swDb.ElapsedMilliseconds;
        }
        else
        {
            (result, dbListMs, dbCountMs) = await GetPagedAccountsFromDatabaseAsync(
                page, pageSize, normalizedSearch, normalizedStatus, includeDeleted,
                cancellationToken);
        }

        var swRedisSet = Stopwatch.StartNew();
        await TrySetCacheAsync(cacheKey, result, AccountListCacheTtl, cancellationToken);
        swRedisSet.Stop();
        redisSetMs = swRedisSet.ElapsedMilliseconds;

        swTotal.Stop();

        if (swTotal.ElapsedMilliseconds >= PerfLogThresholdMs)
        {
            _logger.LogWarning(
                "[PERF] GetPagedAccounts cache=MISS redisGetMs={RedisGetMs} dbListMs={DbListMs} dbCountMs={DbCountMs} redisSetMs={RedisSetMs} totalMs={TotalMs}",
                swRedisGet.Elapsed.TotalMilliseconds,
                dbListMs,
                dbCountMs,
                redisSetMs,
                swTotal.Elapsed.TotalMilliseconds);
        }

        return ServiceResult<PagedResult<AccountDto>>.Success(result);
    }

    public async Task<ServiceResult<AccountSearchStatsDto>> GetSearchStatsAsync(
        bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var aggregation = await _search.AggregateAsync(
                AccountIndexName,
                AccountElasticsearchQueryFactory.BuildStatsAggregation(includeDeleted),
                cancellationToken);

            var roleCounts = await EnrichRoleFacetLabelsAsync(
                SearchFacetMapper.ToDto(
                    aggregation.Facets.GetValueOrDefault(AccountElasticsearchQueryFactory.RoleIdField) ?? []),
                cancellationToken);

            return ServiceResult<AccountSearchStatsDto>.Success(
                new AccountSearchStatsDto
                {
                    StatusCounts = SearchFacetMapper.ToDto(
                        aggregation.Facets.GetValueOrDefault(AccountElasticsearchQueryFactory.StatusField) ?? []),
                    RoleCounts = roleCounts
                },
                "Account search stats retrieved successfully.");
        }
        catch
        {
            return ServiceResult<AccountSearchStatsDto>.Success(
                await GetSearchStatsFromDatabaseAsync(includeDeleted, cancellationToken),
                "Account search stats retrieved successfully.");
        }
    }

    public async Task<ServiceResult<AccountSuggestResponseDto>> SuggestAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ServiceResult<AccountSuggestResponseDto>.BadRequest("Query is required.");
        }

        if (limit is < 1 or > 20)
        {
            return ServiceResult<AccountSuggestResponseDto>.BadRequest("Limit must be between 1 and 20.");
        }

        IReadOnlyList<AccountSuggestItemDto> items;
        try
        {
            var searchResult = await _search.SearchAsync<AccountDto>(
                AccountIndexName,
                AccountElasticsearchQueryFactory.BuildSuggest(query, limit),
                cancellationToken);

            items = searchResult.Documents
                .Select(account => new AccountSuggestItemDto
                {
                    AccountId = account.AccountId,
                    FullName = account.FullName,
                    Email = account.Email
                })
                .ToList();
        }
        catch
        {
            var (fallbackResult, _, _) = await GetPagedAccountsFromDatabaseAsync(1, limit, query.Trim(), null, includeDeleted: false, cancellationToken);
            items = fallbackResult.Items
                .Select(account => new AccountSuggestItemDto
                {
                    AccountId = account.AccountId,
                    FullName = account.FullName,
                    Email = account.Email
                })
                .ToList();
        }

        return ServiceResult<AccountSuggestResponseDto>.Success(
            new AccountSuggestResponseDto { Items = items },
            string.Empty);
    }

    public async Task<ServiceResult<AccountDto>> UpdateAsync(Guid accountId, UpdateAccountRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationErrors = ValidateUpdateRequest(request);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<AccountDto>.BadRequest(validationErrors);
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null || account.DeletedAt is not null)
        {
            return ServiceResult<AccountDto>.NotFound("Account not found.");
        }

        var email = NormalizeEmail(request.Email);
        if (!await _accounts.RoleExistsAsync(request.RoleId, cancellationToken))
        {
            return ServiceResult<AccountDto>.BadRequest("Role does not exist.");
        }

        if (await _accounts.EmailExistsAsync(email, accountId, cancellationToken))
        {
            return ServiceResult<AccountDto>.Conflict("Email already exists.");
        }

        var wasActive = account.Status == AccountStatus.ACTIVE;
        account.RoleId = request.RoleId;
        account.Email = email;
        account.FullName = request.FullName.Trim();
        account.Phone = NormalizeOptional(request.Phone);
        account.AvatarUrl = NormalizeOptional(request.AvatarUrl);
        account.Status = NormalizeStatus(request.Status);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = account.Adapt<AccountDto>();
        await CacheAccountAsync(dto, cancellationToken);
        await InvalidateAccountListsAsync(cancellationToken);
        await IndexAccountAsync(dto, cancellationToken);
        if (wasActive && account.Status != AccountStatus.ACTIVE)
        {
            await _auth.RevokeUserAccessTokensAsync(account.AccountId, cancellationToken);
        }

        return ServiceResult<AccountDto>.Success(dto);
    }

    public async Task<ServiceResult> DeleteAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null || account.DeletedAt is not null)
        {
            return ServiceResult.NotFound("Account not found.");
        }

        account.DeletedAt = DateTime.UtcNow;
        account.Status = AccountStatus.INACTIVE;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await TryRemoveCacheAsync(AccountItemCacheKey(accountId), cancellationToken);
        await InvalidateAccountListsAsync(cancellationToken);
        await TryDeleteIndexAsync(accountId, cancellationToken);
        await _auth.RevokeUserAccessTokensAsync(account.AccountId, cancellationToken);

        return ServiceResult.Success("Account deleted successfully.");
    }

    private async Task<(PagedResult<AccountDto> Result, long DbListMs, long DbCountMs)> GetPagedAccountsFromDatabaseAsync(
        int page,
        int pageSize,
        string? normalizedSearch,
        string? normalizedStatus,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var swList = Stopwatch.StartNew();
        var accounts = await _accounts.GetPagedAsync(page, pageSize, normalizedSearch, normalizedStatus, includeDeleted, cancellationToken);
        swList.Stop();

        var swCount = Stopwatch.StartNew();
        var totalItems = await _accounts.CountAsync(normalizedSearch, normalizedStatus, includeDeleted, cancellationToken);
        swCount.Stop();

        var result = PagedResult<AccountDto>.Create(accounts.Adapt<List<AccountDto>>(), page, pageSize, totalItems);
        return (result, swList.ElapsedMilliseconds, swCount.ElapsedMilliseconds);
    }

    private async Task<PagedResult<AccountDto>> SearchAccountsAsync(
        int page,
        int pageSize,
        string normalizedSearch,
        string? normalizedStatus,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var request = AccountElasticsearchQueryFactory.BuildSearch(
            page,
            pageSize,
            normalizedSearch,
            normalizedStatus,
            includeDeleted);

        try
        {
            var searchResult = await _search.SearchAsync<AccountDto>(AccountIndexName, request, cancellationToken);

            return PagedResult<AccountDto>.Create(
                searchResult.Documents.ToList(),
                page,
                pageSize,
                (int)Math.Min(searchResult.Total, int.MaxValue));
        }
        catch
        {
            var (fallback, _, _) = await GetPagedAccountsFromDatabaseAsync(page, pageSize, normalizedSearch, normalizedStatus, includeDeleted, cancellationToken);
            return fallback;
        }
    }

    private async Task<AccountSearchStatsDto> GetSearchStatsFromDatabaseAsync(
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var statusCounts = (await _accounts.CountGroupedByStatusAsync(includeDeleted, cancellationToken))
            .Select(item => new SearchFacetItemDto
            {
                Key = item.Key,
                Count = item.Count
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roleCounts = await EnrichRoleFacetLabelsAsync(
            (await _accounts.CountGroupedByRoleIdAsync(includeDeleted, cancellationToken))
                .Select(item => new SearchFacetItemDto
                {
                    Key = item.Key,
                    Count = item.Count
                })
                .ToList(),
            cancellationToken);

        return new AccountSearchStatsDto
        {
            StatusCounts = statusCounts,
            RoleCounts = roleCounts
        };
    }

    private async Task<IReadOnlyList<SearchFacetItemDto>> EnrichRoleFacetLabelsAsync(
        IReadOnlyList<SearchFacetItemDto> roleCounts,
        CancellationToken cancellationToken)
    {
        var enriched = new List<SearchFacetItemDto>(roleCounts.Count);
        foreach (var roleCount in roleCounts)
        {
            string? label = null;
            if (Guid.TryParse(roleCount.Key, out var roleId))
            {
                label = await _accounts.GetRoleNameAsync(roleId, cancellationToken);
            }

            enriched.Add(new SearchFacetItemDto
            {
                Key = roleCount.Key,
                Count = roleCount.Count,
                Label = label
            });
        }

        return enriched;
    }

    private async Task CacheAccountAsync(AccountDto account, CancellationToken cancellationToken)
    {
        await TrySetCacheAsync(AccountItemCacheKey(account.AccountId), account, AccountItemCacheTtl, cancellationToken);
    }

    private async Task InvalidateAccountListsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RemoveByPrefixAsync(AccountListCachePrefix, cancellationToken);
        }
        catch
        {
            // Cache invalidation should not fail the database-backed account workflow.
        }
    }

    private async Task IndexAccountAsync(AccountDto account, CancellationToken cancellationToken)
    {
        try
        {
            if (account.DeletedAt is not null)
            {
                await _search.DeleteAsync(AccountIndexName, account.AccountId.ToString(), cancellationToken);
                return;
            }

            await _search.IndexAsync(AccountIndexName, account.AccountId.ToString(), account, cancellationToken);
        }
        catch
        {
            // Search indexing is eventually consistent and should not fail the database write.
        }
    }

    private async Task TryDeleteIndexAsync(Guid accountId, CancellationToken cancellationToken)
    {
        try
        {
            await _search.DeleteAsync(AccountIndexName, accountId.ToString(), cancellationToken);
        }
        catch
        {
            // Search indexing is eventually consistent and should not fail the database write.
        }
    }

    private async Task<T?> TryGetCacheAsync<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await _cache.GetAsync<T>(key, cancellationToken);
        }
        catch
        {
            return default;
        }
    }

    private async Task TrySetCacheAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken)
    {
        try
        {
            await _cache.SetAsync(key, value, expiration, cancellationToken);
        }
        catch
        {
            // Cache writes are best-effort.
        }
    }

    private async Task TryRemoveCacheAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch
        {
            // Cache removals are best-effort.
        }
    }

    private static string AccountItemCacheKey(Guid accountId)
    {
        return $"{AccountItemCachePrefix}{accountId}";
    }

    private static string AccountListCacheKey(int page, int pageSize, string? search, string? status, bool includeDeleted)
    {
        var value = $"{page}|{pageSize}|{search}|{status}|{includeDeleted}";
        return $"{AccountListCachePrefix}{Sha256(value)}";
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static List<string> ValidateCreateRequest(CreateAccountRequestDto request)
    {
        var errors = ValidateCommon(request.RoleId, request.Email, request.FullName, request.Status);
        if (string.IsNullOrWhiteSpace(request.PasswordHash))
        {
            errors.Add("Password hash is required.");
        }
        else if (request.PasswordHash.Length > 255)
        {
            errors.Add("Password hash must not exceed 255 characters.");
        }

        return errors;
    }

    private static List<string> ValidateUpdateRequest(UpdateAccountRequestDto request)
    {
        return ValidateCommon(request.RoleId, request.Email, request.FullName, request.Status);
    }

    private static List<string> ValidateProfileUpdateRequest(UpdateMyProfileRequestDto request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors.Add("Full name is required.");
        }
        else if (request.FullName.Trim().Length > 100)
        {
            errors.Add("Full name must not exceed 100 characters.");
        }

        if (request.Phone?.Trim().Length > 20)
        {
            errors.Add("Phone must not exceed 20 characters.");
        }

        return errors;
    }

    private static List<string> ValidateCommon(Guid roleId, string email, string fullName, string? status)
    {
        var errors = new List<string>();
        if (roleId == Guid.Empty)
        {
            errors.Add("Role id is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add("Email is required.");
        }
        else if (email.Length > 100)
        {
            errors.Add("Email must not exceed 100 characters.");
        }
        else if (!email.Contains('@', StringComparison.Ordinal))
        {
            errors.Add("Email is invalid.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors.Add("Full name is required.");
        }
        else if (fullName.Length > 100)
        {
            errors.Add("Full name must not exceed 100 characters.");
        }

        if (!TryNormalizeStatus(status, out _))
        {
            errors.Add("Status is invalid.");
        }

        return errors;
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static AccountStatus NormalizeStatus(string? status)
    {
        return TryNormalizeStatus(status, out var normalizedStatus)
            ? Enum.Parse<AccountStatus>(normalizedStatus ?? AccountStatus.ACTIVE.ToString())
            : AccountStatus.ACTIVE;
    }

    private static bool TryNormalizeStatus(string? status, out string? normalizedStatus)
    {
        var value = NormalizeOptional(status);
        if (value is null)
        {
            normalizedStatus = null;
            return true;
        }

        if (!Enum.TryParse<AccountStatus>(value, ignoreCase: true, out var accountStatus))
        {
            normalizedStatus = null;
            return false;
        }

        normalizedStatus = accountStatus.ToString();
        return true;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
