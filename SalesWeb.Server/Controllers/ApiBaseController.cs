using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SalesWeb.Server.Services;

namespace SalesWeb.Server.Controllers;

/// <summary>Base cho các API controller — truy cập user đã xác thực.</summary>
[ApiController]
[EnableRateLimiting("api")]
public abstract class ApiBaseController : ControllerBase
{
    protected AppUser CurrentUser =>
        (AppUser)HttpContext.Items["AppUser"]!;

    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
