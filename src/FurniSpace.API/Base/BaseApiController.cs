using Microsoft.AspNetCore.Mvc;
using FurniSpace.Application.Common;

namespace FurniSpace.API.Base;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult ToActionResult(IServiceResult result)
    {
        return StatusCode(result.Status, result);
    }
}
