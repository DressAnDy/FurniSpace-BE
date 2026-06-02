#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

public sealed class AccountsController : BaseApiController
{
    private readonly IAccountService _accounts;

    public AccountsController(IAccountService accounts)
    {
        _accounts = accounts;
    }

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

    [HttpGet("{accountId:guid}")]
    public async Task<IActionResult> GetById(Guid accountId, CancellationToken cancellationToken)
    {
        var result = await _accounts.GetByIdAsync(accountId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _accounts.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{accountId:guid}")]
    public async Task<IActionResult> Update(Guid accountId, [FromBody] UpdateAccountRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _accounts.UpdateAsync(accountId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{accountId:guid}")]
    public async Task<IActionResult> Delete(Guid accountId, CancellationToken cancellationToken)
    {
        var result = await _accounts.DeleteAsync(accountId, cancellationToken);
        return ToActionResult(result);
    }
}
