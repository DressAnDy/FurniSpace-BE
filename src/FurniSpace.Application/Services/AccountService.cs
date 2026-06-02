using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.Interfaces;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services;

public sealed class AccountService : IAccountService
{
    private readonly IAccountRepository _accounts;

    public AccountService(IAccountRepository accounts)
    {
        _accounts = accounts;
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

        return ServiceResult<AccountDto>.Created(account.Adapt<AccountDto>());
    }

    public async Task<ServiceResult<AccountDto>> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null || account.DeletedAt is not null)
        {
            return ServiceResult<AccountDto>.NotFound("Account not found.");
        }

        return ServiceResult<AccountDto>.Success(account.Adapt<AccountDto>());
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
        var accounts = await _accounts.GetPagedAsync(page, pageSize, normalizedSearch, normalizedStatus, includeDeleted, cancellationToken);
        var totalItems = await _accounts.CountAsync(normalizedSearch, normalizedStatus, includeDeleted, cancellationToken);
        var result = PagedResult<AccountDto>.Create(accounts.Adapt<List<AccountDto>>(), page, pageSize, totalItems);

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

        return ServiceResult<AccountDto>.Success(account.Adapt<AccountDto>());
    }

    public async Task<ServiceResult> DeleteAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null || account.DeletedAt is not null)
        {
            return ServiceResult.NotFound("Account not found.");
        }

        account.DeletedAt = DateTime.UtcNow;
        account.Status = AccountStatus.INACTIVE.ToString();
        await _accounts.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success("Account deleted successfully.");
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

    private static string NormalizeStatus(string? status)
    {
        return TryNormalizeStatus(status, out var normalizedStatus)
            ? normalizedStatus ?? AccountStatus.ACTIVE.ToString()
            : AccountStatus.ACTIVE.ToString();
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
