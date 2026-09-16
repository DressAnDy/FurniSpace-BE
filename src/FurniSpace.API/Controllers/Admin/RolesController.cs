#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.Interfaces.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
[Route("admin/roles")]
public sealed class RolesController : BaseApiController
{
    private readonly IAccountService _accounts;

    public RolesController(IAccountService accounts)
    {
        _accounts = accounts;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var result = await _accounts.GetAllRolesAsync(cancellationToken);
        return ToActionResult(result);
    }
}
