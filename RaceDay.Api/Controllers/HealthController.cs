using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace RaceDay.Api.Controllers;

[Route("api/health")]
public class HealthController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Liveness probe that also confirms the database connection.</summary>
    [HttpGet, AllowAnonymous]
    [ProducesResponseType(Status200OK)]
    [ProducesResponseType(Status503ServiceUnavailable)]
    public async Task<IActionResult> Get()
    {
        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        var connected = await db.Database.CanConnectAsync();
        return connected
            ? Ok(new { status = "Healthy", database = "Connected", version })
            : StatusCode(Status503ServiceUnavailable, new { status = "Unhealthy", database = "Unreachable", version });
    }
}
