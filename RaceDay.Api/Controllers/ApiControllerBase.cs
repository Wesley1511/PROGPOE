using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected int? CurrentUserIdOrNull => User.Identity?.IsAuthenticated == true ? CurrentUserId : null;

    protected ObjectResult Fail(int status, string detail) => Problem(detail: detail, statusCode: status);
}
