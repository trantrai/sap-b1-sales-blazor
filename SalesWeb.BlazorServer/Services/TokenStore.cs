using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.BlazorServer.Services;

/// <summary>
/// Lưu/đọc JWT + thông tin user trong sessionStorage của trình duyệt
/// (mã hoá qua ASP.NET Core Data Protection).
///
/// Blazor Server chạy phía server nên không gọi localStorage trực tiếp được;
/// dùng <see cref="ProtectedSessionStorage"/> thay cho localStorage như bản WebAssembly.
///
/// Lưu ý prerender: ProtectedSessionStorage cần JS interop nên chưa dùng được
/// trong lần prerender đầu — các hàm Get trả về null và Set bỏ qua trong
/// trường hợp này (bắt InvalidOperationException). Sau khi circuit interactive
/// được thiết lập, Blazor render lại và đọc đúng giá trị đã lưu.
/// </summary>
public class TokenStore(ProtectedSessionStorage storage)
{
    private const string TokenKey = "sw_token";
    private const string RefreshKey = "sw_refresh";
    private const string UserKey = "sw_user";

    public async Task<string?> GetTokenAsync()
    {
        try { return (await storage.GetAsync<string>(TokenKey)).Value; }
        catch (InvalidOperationException) { return null; } // đang prerender
    }

    public async Task SetTokenAsync(string? token)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
                await storage.DeleteAsync(TokenKey);
            else
                await storage.SetAsync(TokenKey, token);
        }
        catch (InvalidOperationException) { /* đang prerender: bỏ qua */ }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try { return (await storage.GetAsync<string>(RefreshKey)).Value; }
        catch (InvalidOperationException) { return null; }
    }

    public async Task SetRefreshTokenAsync(string? token)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
                await storage.DeleteAsync(RefreshKey);
            else
                await storage.SetAsync(RefreshKey, token);
        }
        catch (InvalidOperationException) { /* đang prerender: bỏ qua */ }
    }

    public async Task ClearAsync()
    {
        try
        {
            await storage.DeleteAsync(TokenKey);
            await storage.DeleteAsync(RefreshKey);
            await storage.DeleteAsync(UserKey);
        }
        catch (InvalidOperationException) { /* đang prerender: bỏ qua */ }
    }

    public async Task<UserOut?> GetUserAsync()
    {
        try { return (await storage.GetAsync<UserOut>(UserKey)).Value; }
        catch (InvalidOperationException) { return null; }
    }

    public async Task SetUserAsync(UserOut? user)
    {
        try
        {
            if (user is null)
                await storage.DeleteAsync(UserKey);
            else
                await storage.SetAsync(UserKey, user);
        }
        catch (InvalidOperationException) { /* đang prerender: bỏ qua */ }
    }
}
