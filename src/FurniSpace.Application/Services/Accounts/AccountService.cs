using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.Interfaces.Accounts;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
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
    private static readonly TimeSpan AccountItemCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AccountListCacheTtl = TimeSpan.FromMinutes(2);

    private readonly IAccountRepository _accounts;
    private readonly InfrastructureCacheService _cache;
    private readonly InfrastructureSearchIndexService _search;

    public AccountService(
        IAccountRepository accounts,
        InfrastructureCacheService cache,
        InfrastructureSearchIndexService search)
    {
        _accounts = accounts;
        _cache = cache;
        _search = search;
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
        await _accounts.SaveChangesAsync(cancellationToken);

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
        var cached = await TryGetCacheAsync<PagedResult<AccountDto>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return ServiceResult<PagedResult<AccountDto>>.Success(cached);
        }

        var result = !string.IsNullOrWhiteSpace(normalizedSearch)
            ? await SearchAccountsAsync(page, pageSize, normalizedSearch, normalizedStatus, includeDeleted, cancellationToken)
            : await GetPagedAccountsFromDatabaseAsync(page, pageSize, normalizedSearch, normalizedStatus, includeDeleted, cancellationToken);

        await TrySetCacheAsync(cacheKey, result, AccountListCacheTtl, cancellationToken);

        return ServiceResult<PagedResult<AccountDto>>.Success(result);
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

        account.RoleId = request.RoleId;
        account.Email = email;
        account.FullName = request.FullName.Trim();
        account.Phone = NormalizeOptional(request.Phone);
        account.AvatarUrl = NormalizeOptional(request.AvatarUrl);
        account.Status = NormalizeStatus(request.Status);

        await _accounts.SaveChangesAsync(cancellationToken);

        var dto = account.Adapt<AccountDto>();
        await CacheAccountAsync(dto, cancellationToken);
        await InvalidateAccountListsAsync(cancellationToken);
        await IndexAccountAsync(dto, cancellationToken);

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
        await _accounts.SaveChangesAsync(cancellationToken);

        await TryRemoveCacheAsync(AccountItemCacheKey(accountId), cancellationToken);
        await InvalidateAccountListsAsync(cancellationToken);
        await TryDeleteIndexAsync(accountId, cancellationToken);

        return ServiceResult.Success("Account deleted successfully.");
    }

    private async Task<PagedResult<AccountDto>> GetPagedAccountsFromDatabaseAsync(
        int page,
        int pageSize,
        string? normalizedSearch,
        string? normalizedStatus,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var accounts = await _accounts.GetPagedAsync(page, pageSize, normalizedSearch, normalizedStatus, includeDeleted, cancellationToken);
        var totalItems = await _accounts.CountAsync(normalizedSearch, normalizedStatus, includeDeleted, cancellationToken);

        return PagedResult<AccountDto>.Create(accounts.Adapt<List<AccountDto>>(), page, pageSize, totalItems);
    }

    private async Task<PagedResult<AccountDto>> SearchAccountsAsync(
        int page,
        int pageSize,
        string normalizedSearch,
        string? normalizedStatus,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var query = BuildAccountSearchQuery(normalizedSearch);
        var searchSize = Math.Max(page * pageSize, 100);
        IReadOnlyList<AccountDto> searchResults;
        try
        {
            searchResults = await _search.SearchAsync<AccountDto>(AccountIndexName, query, searchSize, cancellationToken);
        }
        catch
        {
            return await GetPagedAccountsFromDatabaseAsync(page, pageSize, normalizedSearch, normalizedStatus, includeDeleted, cancellationToken);
        }

        var filtered = searchResults
            .Where(account => includeDeleted || account.DeletedAt is null)
            .Where(account => normalizedStatus is null || account.Status == normalizedStatus)
            .OrderByDescending(account => account.CreatedAt)
            .ThenBy(account => account.Email)
            .ToList();

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return PagedResult<AccountDto>.Create(items, page, pageSize, filtered.Count);
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

    private static string BuildAccountSearchQuery(string search)
    {
        var value = EscapeQueryString(search);
        return $"email:*{value}* OR fullName:*{value}* OR phone:*{value}*";
    }

    private static string EscapeQueryString(string value)
    {
        var reservedChars = new[] { "\\", "+", "-", "=", "&&", "||", ">", "<", "!", "(", ")", "{", "}", "[", "]", "^", "\"", "~", "*", "?", ":", "/", " " };
        var escaped = value.Trim();
        foreach (var reservedChar in reservedChars)
        {
            escaped = escaped.Replace(reservedChar, $"\\{reservedChar}", StringComparison.Ordinal);
        }

        return escaped;
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
