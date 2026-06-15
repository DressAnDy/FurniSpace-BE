#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.Interfaces.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FurniSpace.API.Controllers;

public sealed class AccountsController : BaseApiController
{
    private readonly IAccountService _accounts;

    public AccountsController(IAccountService accounts)
    {
        _accounts = accounts;
    }

    /// <summary>
    /// Lấy danh sách tài khoản có phân trang và bộ lọc tùy chọn.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _accounts.GetPagedAsync(page, pageSize, search, status, includeDeleted, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Lấy chi tiết tài khoản theo account id.
    /// </summary>
    [HttpGet("{accountId:guid}")]
    public async Task<IActionResult> GetById(Guid accountId, CancellationToken cancellationToken)
    {
        var result = await _accounts.GetByIdAsync(accountId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Lấy chi tiết tài khoản dành cho admin kèm các trường nội bộ.
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    [HttpGet("/admin/accounts/{accountId:guid}")]
    public async Task<IActionResult> GetAdminDetail(Guid accountId, CancellationToken cancellationToken = default)
    {
        var result = await _accounts.GetAdminDetailAsync(accountId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Cập nhật thông tin hồ sơ của người dùng hiện tại đã đăng nhập.
    /// </summary>
    [Authorize]
    [HttpPatch("/accounts/me")]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdateMyProfileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return TryGetCurrentUserId(out var currentUserId)
            ? ToActionResult(await _accounts.UpdateMyProfileAsync(currentUserId, request, cancellationToken))
            : ToActionResult(ServiceResult.Unauthorized());
    }

    /// <summary>
    /// Tạo tài khoản mới.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _accounts.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Cập nhật thông tin tài khoản theo account id.
    /// </summary>
    [HttpPut("{accountId:guid}")]
    public async Task<IActionResult> Update(Guid accountId, [FromBody] UpdateAccountRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _accounts.UpdateAsync(accountId, request, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Xóa mềm tài khoản theo account id.
    /// </summary>
    [HttpDelete("{accountId:guid}")]
    public async Task<IActionResult> Delete(Guid accountId, CancellationToken cancellationToken)
    {
        var result = await _accounts.DeleteAsync(accountId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
