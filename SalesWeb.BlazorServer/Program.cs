using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.FluentUI.AspNetCore.Components;
using SalesWeb.BlazorServer.Components;
using SalesWeb.BlazorServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();
builder.Services.AddAuthorizationCore();

// Base URL của API backend: đọc từ appsettings ("ApiBaseUrl").
// Mặc định local: http://localhost:5050 (SalesWeb.Server).
var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
var apiBase = string.IsNullOrWhiteSpace(apiBaseUrl)
    ? "http://localhost:5050/"
    : apiBaseUrl.TrimEnd('/') + "/";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });

// ProtectedBrowserStorage: TokenStore dùng ProtectedSessionStorage thay cho
// localStorage của bản WebAssembly (Blazor Server chạy phía server).
builder.Services.AddScoped<ProtectedSessionStorage>();

builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<MasterDataCache>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthState>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
