using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SalesWeb.Server.Data;
using SalesWeb.Server.Middleware;
using SalesWeb.Server.Services;
using SalesWeb.Server.Services.B1;
using SalesWeb.Shared;

var builder = WebApplication.CreateBuilder(args);

// ---------- Cấu hình ----------
var appConfig = AppConfig.Load(builder.Configuration);
builder.Services.AddSingleton(appConfig);

// ---------- Database ----------
Db.Configure(appConfig.DataDir);
Db.InitAll(appConfig);

// ---------- Services ----------
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<ConnService>();
builder.Services.AddSingleton<SyncService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<B1ClientPool>();
// IB1Client theo request: proxy điều hướng tới client của đúng CompanyDB
// mà user đã chọn lúc đăng nhập (claim b1_db trong JWT)
builder.Services.AddScoped<IB1Client, B1ClientProxy>();
builder.Services.AddHostedService<BackupService>();

// ---------- JWT bearer ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appConfig.JwtSecret)),
            ClockSkew = TimeSpan.Zero,
        };
    });
builder.Services.AddAuthorization();

// ---------- Controllers + JSON snake_case ----------
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance;
    o.JsonSerializerOptions.DictionaryKeyPolicy = SnakeCaseNamingPolicy.Instance;
    o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var msg = string.Join("; ", ctx.ModelState.Values
            .SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return new BadRequestObjectResult(new
        {
            detail = string.IsNullOrEmpty(msg) ? "Dữ liệu không hợp lệ." : msg,
        });
    };
});

builder.Services.AddOpenApi();

// ---------- Rate limiting (B7) ----------
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Chính sách chung: 300 req/phút/IP
    o.AddPolicy("api", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    // Đăng nhập: 10 lần/phút/IP — chống dò mật khẩu
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

// ---------- Pipeline ----------
app.UseApiErrors();

// Fix #11: nhận IP thật khi chạy sau reverse proxy (cấu hình TRUSTED_PROXIES nếu cần)
var fwdOpts = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
// Mặc định trust loopback/docker network; production nên set TRUSTED_PROXIES rõ ràng
fwdOpts.KnownNetworks.Clear();
fwdOpts.KnownProxies.Clear();
var trusted = Environment.GetEnvironmentVariable("TRUSTED_PROXIES");
if (!string.IsNullOrWhiteSpace(trusted))
{
    foreach (var ip in trusted.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        if (System.Net.IPAddress.TryParse(ip, out var addr))
            fwdOpts.KnownProxies.Add(addr);
}
app.UseForwardedHeaders(fwdOpts);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// ---------- Khởi tạo B1 client ----------
var pool = app.Services.GetRequiredService<B1ClientPool>();
var connSvc = app.Services.GetRequiredService<ConnService>();
var defaultClient = pool.GetClient(null);
var eff = connSvc.GetEffective();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation("Khởi động xong — chế độ: {Mode}",
    eff.B1Mock ? "MOCK (dữ liệu giả)" : $"B1 thật ({eff.B1ServiceLayerUrl})");
if (eff.B1Mock)
{
    // Mock: dữ liệu trong RAM nên tự đồng bộ vào DB local để demo tra cứu nhanh
    var sync = app.Services.GetRequiredService<SyncService>();
    await sync.SyncManyAsync(new[] { "customers", "items", "tax_codes" }, defaultClient);
    logger.LogInformation("Đã nạp master data mock vào DB local");
}

// ---------- React client (thay Blazor) ----------
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
