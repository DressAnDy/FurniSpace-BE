using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Accounts;

namespace FurniSpace.Application.Interfaces.Accounts;

public interface IAccountService
{
    Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AccountDto>> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<ServiceResult<AccountDetailDto>> GetAdminDetailAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<ServiceResult<MyProfileDto>> UpdateMyProfileAsync(Guid currentUserId, UpdateMyProfileRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<PagedResult<AvailableDesignerDto>>> GetAvailableDesignersAsync(AvailableDesignerQueryDto query, CancellationToken cancellationToken = default);
    Task<ServiceResult<PagedResult<AccountDto>>> GetPagedAsync(int page, int pageSize, string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default);
    Task<ServiceResult<AccountSearchStatsDto>> GetSearchStatsAsync(bool includeDeleted, CancellationToken cancellationToken = default);
    Task<ServiceResult<AccountSuggestResponseDto>> SuggestAsync(string query, int limit, CancellationToken cancellationToken = default);
    Task<ServiceResult<AccountDto>> UpdateAsync(Guid accountId, UpdateAccountRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(Guid accountId, CancellationToken cancellationToken = default);
}
