using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
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
builder.Services.AddSingleton<B1ClientHolder>();
builder.Services.AddSingleton<IB1Client>(sp => sp.GetRequiredService<B1ClientHolder>().Current);

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

var app = builder.Build();

// ---------- Pipeline ----------
app.UseApiErrors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ---------- Khởi tạo B1 client ----------
var holder = app.Services.GetRequiredService<B1ClientHolder>();
var connSvc = app.Services.GetRequiredService<ConnService>();
holder.Current = connSvc.BuildClient();
var eff = connSvc.GetEffective();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation("Khởi động xong — chế độ: {Mode}",
    eff.B1Mock ? "MOCK (dữ liệu giả)" : $"B1 thật ({eff.B1ServiceLayerUrl})");
if (eff.B1Mock)
{
    // Mock: dữ liệu trong RAM nên tự đồng bộ vào DB local để demo tra cứu nhanh
    var sync = app.Services.GetRequiredService<SyncService>();
    await sync.SyncManyAsync(new[] { "customers", "items", "tax_codes" }, holder.Current);
    logger.LogInformation("Đã nạp master data mock vào DB local");
}

// ---------- Blazor client ----------
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
