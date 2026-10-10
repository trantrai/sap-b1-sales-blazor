using System.Text.Json;

namespace SalesWeb.Server.Middleware;

/// <summary>Lỗi HTTP có chủ đích — dịch thành {"detail": ...} như FastAPI HTTPException.</summary>
public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}

public static class ApiExceptionMiddleware
{
    public static IApplicationBuilder UseApiErrors(this IApplicationBuilder app)
    {
        return app.Use(async (ctx, next) =>
        {
            try
            {
                await next();
            }
            catch (ApiException ex)
            {
                ctx.Response.StatusCode = ex.StatusCode;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                    new { detail = ex.Message }, Services.ServerJson.Options));
            }
            catch (Exception ex)
            {
                var logger = ctx.RequestServices.GetService<ILoggerFactory>()
                    ?.CreateLogger("ApiErrors");
                logger?.LogError(ex, "Lỗi không mong đợi");
                if (!ctx.Response.HasStarted)
                {
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                        new { detail = "Lỗi hệ thống, vui lòng thử lại sau." },
                        Services.ServerJson.Options));
                }
            }
        });
    }
}
