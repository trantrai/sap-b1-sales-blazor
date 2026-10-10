using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using SalesWeb.Client;
using SalesWeb.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Base URL của API: đọc từ appsettings.json ("ApiBaseUrl").
// Để trống = cùng origin với frontend (API + Blazor chung một server).
// Khi deploy frontend tĩnh (Vercel) tách khỏi API, điền URL tuyệt đối của API.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
var apiBase = string.IsNullOrWhiteSpace(apiBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : apiBaseUrl.TrimEnd('/') + "/";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddFluentUIComponents();
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthState>());
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
