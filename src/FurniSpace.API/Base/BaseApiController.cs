using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Base;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
}
