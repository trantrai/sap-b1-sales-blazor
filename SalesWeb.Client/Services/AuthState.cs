using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Client.Services;

/// <summary>
/// AuthenticationStateProvider đọc JWT từ localStorage (qua IJSRuntime).
/// Phân cấp role: viewer &lt; sales &lt; manager &lt; admin.
/// </summary>
public class AuthState : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private readonly TokenStore _tokens;
    private readonly ApiClient _api;

    public UserOut? User { get; private set; }

    public AuthState(TokenStore tokens, ApiClient api)
    {
        _tokens = tokens;
        _api = api;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokens.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
            return new AuthenticationState(Anonymous);

        if (User is null)
        {
            try
            {
                User = await _api.GetMeAsync();
                await _tokens.SetUserAsync(User);
            }
            catch
            {
                await LogoutAsync(silent: true);
                return new AuthenticationState(Anonymous);
            }
        }
        return new AuthenticationState(BuildPrincipal(User!));
    }

    public async Task LoginAsync(string username, string password)
    {
        var t = await _api.LoginAsync(username, password)
            ?? throw new ApiException(401, "Đăng nhập thất bại.");
        if (string.IsNullOrEmpty(t.AccessToken))
            throw new ApiException(401, "Đăng nhập thất bại.");
        await _tokens.SetTokenAsync(t.AccessToken);
        User = await _api.GetMeAsync()
            ?? throw new ApiException(401, "Không lấy được thông tin người dùng.");
        await _tokens.SetUserAsync(User);
        NotifyAuthenticationStateChanged(
            Task.FromResult(new AuthenticationState(BuildPrincipal(User))));
    }

    public async Task LogoutAsync(bool silent = false)
    {
        await _tokens.SetTokenAsync(null);
        await _tokens.SetUserAsync(null);
        User = null;
        if (!silent)
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    /// <summary>True nếu role hiện tại có cấp &gt;= minRole (viewer&lt;sales&lt;manager&lt;admin).</summary>
    public bool HasRole(string minRole) => Rank(User?.Role) >= Rank(minRole);

    public static int Rank(string? role) => role switch
    {
        "admin" => 3,
        "manager" => 2,
        "sales" => 1,
        _ => 0,
    };

    private static ClaimsPrincipal BuildPrincipal(UserOut u)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, u.Username),
            new Claim(ClaimTypes.Role, u.Role),
            new Claim("full_name", u.FullName ?? u.Username),
        }, "jwt");
        return new ClaimsPrincipal(identity);
    }
}
