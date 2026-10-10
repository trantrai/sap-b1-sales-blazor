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
    public string AdminPassword { get; init; } = "admin123";
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
        return new AppConfig
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
