using Microsoft.JSInterop;
using SalesWeb.Shared.Dtos;
using SalesWeb.Client.Services;

namespace SalesWeb.Client.Services;

/// <summary>Lưu/đọc JWT + thông tin user trong localStorage (khóa như bản React: sw_token).</summary>
public class TokenStore(IJSRuntime js)
{
    private const string TokenKey = "sw_token";
    private const string RefreshKey = "sw_refresh";
    private const string UserKey = "sw_user";

    public ValueTask<string?> GetTokenAsync()
        => js.InvokeAsync<string?>("localStorage.getItem", TokenKey);

    public ValueTask SetTokenAsync(string? token)
        => string.IsNullOrEmpty(token)
            ? js.InvokeVoidAsync("localStorage.removeItem", TokenKey)
            : js.InvokeVoidAsync("localStorage.setItem", TokenKey, token);

    public ValueTask<string?> GetRefreshTokenAsync()
        => js.InvokeAsync<string?>("localStorage.getItem", RefreshKey);

    public ValueTask SetRefreshTokenAsync(string? token)
        => string.IsNullOrEmpty(token)
            ? js.InvokeVoidAsync("localStorage.removeItem", RefreshKey)
            : js.InvokeVoidAsync("localStorage.setItem", RefreshKey, token);

    public async Task ClearAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await js.InvokeVoidAsync("localStorage.removeItem", RefreshKey);
        await js.InvokeVoidAsync("localStorage.removeItem", UserKey);
    }

    public async Task<UserOut?> GetUserAsync()
    {
        var s = await js.InvokeAsync<string?>("localStorage.getItem", UserKey);
        return string.IsNullOrEmpty(s)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<UserOut>(s, ApiClient.JsonOpts);
    }

    public ValueTask SetUserAsync(UserOut? user)
        => user is null
            ? js.InvokeVoidAsync("localStorage.removeItem", UserKey)
            : js.InvokeVoidAsync("localStorage.setItem", UserKey,
                System.Text.Json.JsonSerializer.Serialize(user, ApiClient.JsonOpts));
}
