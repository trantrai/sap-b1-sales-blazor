using System.Text.Json;
using SalesWeb.Shared;

namespace SalesWeb.Server.Services;

/// <summary>Cấu hình service — đọc từ appsettings.json, biến môi trường kiểu Python (B1_MOCK, ...) ghi đè.</summary>
public sealed class AppConfig
{
    public bool B1Mock { get; init; }
    public string B1ServiceLayerUrl { get; init; } = "https://your-b1-server:50000/b1s/v2";
    public string B1CompanyDb { get; init; } = "SBODEMO_VN";
    public string B1Username { get; init; } = "manager";
    public string B1Password { get; init; } = "changeme";
    public bool B1VerifySsl { get; init; } = true;
    public double B1Timeout { get; init; } = 30;
    public string ExternalIdUdf { get; init; } = "U_ExternalID";
    public string JwtSecret { get; init; } = "demo-secret-32-chars-minimum-xxxxx";
    public int JwtExpireMinutes { get; init; } = 480;
    public string AdminUsername { get; init; } = "admin";
    public string AdminPassword { get; private set; } = "admin123";
    public string DataDir { get; init; } = "./data";

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name);

    private static bool EnvBool(string name, bool dflt)
    {
        var v = Env(name);
        if (v is null) return dflt;
        return v.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
    }

    private static double EnvDouble(string name, double dflt)
        => double.TryParse(Env(name), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : dflt;

    private static int EnvInt(string name, int dflt)
        => int.TryParse(Env(name), out var v) ? v : dflt;

    public static AppConfig Load(IConfiguration cfg)
    {
        string S(string key, string dflt) => cfg[key] ?? dflt;
        var config = new AppConfig
        {
            B1Mock = EnvBool("B1_MOCK", cfg.GetValue("B1:Mock", true)),
            B1ServiceLayerUrl = Env("B1_SERVICE_LAYER_URL") ?? S("B1:ServiceLayerUrl", "https://your-b1-server:50000/b1s/v2"),
            B1CompanyDb = Env("B1_COMPANY_DB") ?? S("B1:CompanyDb", "SBODEMO_VN"),
            B1Username = Env("B1_USERNAME") ?? S("B1:Username", "manager"),
            B1Password = Env("B1_PASSWORD") ?? S("B1:Password", "changeme"),
            B1VerifySsl = EnvBool("B1_VERIFY_SSL", cfg.GetValue("B1:VerifySsl", true)),
            B1Timeout = EnvDouble("B1_TIMEOUT", cfg.GetValue("B1:Timeout", 30.0)),
            ExternalIdUdf = Env("EXTERNAL_ID_UDF") ?? S("ExternalIdUdf", "U_ExternalID"),
            JwtSecret = Env("JWT_SECRET") ?? S("Jwt:Secret", "demo-secret-32-chars-minimum-xxxxx"),
            JwtExpireMinutes = EnvInt("JWT_EXPIRE_MINUTES", cfg.GetValue("Jwt:ExpireMinutes", 480)),
            AdminUsername = Env("ADMIN_USERNAME") ?? S("Admin:Username", "admin"),
            AdminPassword = Env("ADMIN_PASSWORD") ?? S("Admin:Password", "admin123"),
            DataDir = Env("DATA_DIR") ?? S("DataDir", "./data"),
        };
        config.Validate();
        return config;
    }

    /// <summary>
    /// Fail-fast: khi chạy B1 thật (production) phải có JWT secret đủ mạnh.
    /// Chế độ mock/demo vẫn chạy được với giá trị mặc định để dễ thử nghiệm.
    /// </summary>
    private void Validate()
    {
        if (B1Mock)
        {
            // Demo mode: placeholder → dùng admin123 cho tiện thử nghiệm
            if (AdminPassword == "CHANGE_ME_IN_PRODUCTION")
                AdminPassword = "admin123";
            return;
        }
        if (string.IsNullOrWhiteSpace(JwtSecret) || JwtSecret.Length < 32 ||
            JwtSecret.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
            JwtSecret == "demo-secret-32-chars-minimum-xxxxx")
            throw new InvalidOperationException(
                "JWT_SECRET is required (>= 32 chars, not the demo value) when B1_MOCK=false.");
        if (AdminPassword == "admin123" ||
            AdminPassword.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "ADMIN_PASSWORD must be changed from the default when B1_MOCK=false.");
        // M3: validate B1 live settings — fail-fast toàn bộ
        if (B1Password == "changeme" ||
            B1Password.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "B1_PASSWORD must be set to the real Service Layer password when B1_MOCK=false.");
        if (B1ServiceLayerUrl.Contains("your-b1-server", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "B1_SERVICE_LAYER_URL must be set to the real server when B1_MOCK=false.");
        if (!Uri.TryCreate(B1ServiceLayerUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
            throw new InvalidOperationException(
                "B1_SERVICE_LAYER_URL must be a valid http(s) URL when B1_MOCK=false.");
        if (string.IsNullOrWhiteSpace(B1CompanyDb))
            throw new InvalidOperationException(
                "B1_COMPANY_DB is required when B1_MOCK=false.");
        if (string.IsNullOrWhiteSpace(B1Username))
            throw new InvalidOperationException(
                "B1_USERNAME is required when B1_MOCK=false.");
        // SSL verification chỉ được tắt ở Development hoặc có cờ xác nhận rõ ràng
        if (!B1VerifySsl && Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development" &&
            Environment.GetEnvironmentVariable("B1_ALLOW_INSECURE_SSL") != "1")
            throw new InvalidOperationException(
                "B1_VERIFY_SSL=false requires ASPNETCORE_ENVIRONMENT=Development or B1_ALLOW_INSECURE_SSL=1.");
    }
}

/// <summary>JsonSerializerOptions dùng chung: snake_case như API Python.</summary>
public static class ServerJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance,
        DictionaryKeyPolicy = SnakeCaseNamingPolicy.Instance,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    public static readonly System.Text.Encodings.Web.JavaScriptEncoder Relaxed =
        System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
}
