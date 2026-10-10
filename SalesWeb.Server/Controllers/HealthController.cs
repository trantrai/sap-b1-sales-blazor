using Microsoft.AspNetCore.Mvc;
using SalesWeb.Server.Services;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Controllers;

public sealed class HealthController : ControllerBase
{
    private readonly ConnService _conn;
    public HealthController(ConnService conn) => _conn = conn;

    [HttpGet("/health")]
    public ActionResult<HealthOut> Health()
    {
        var eff = _conn.GetEffective();
        return Ok(new HealthOut
        {
            Status = "ok",
            B1Mode = eff.B1Mock ? "mock" : "live",
            B1Url = eff.B1Mock ? null : eff.B1ServiceLayerUrl,
        });
    }
}
