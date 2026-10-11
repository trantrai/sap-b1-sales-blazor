namespace SalesWeb.Shared.Dtos;

public class LoginIn
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>Mã CompanyDB chọn lúc đăng nhập (rỗng = dùng mặc định của hệ thống).</summary>
    public string CompanyDb { get; set; } = "";
}

public class TokenOut
{
    public string AccessToken { get; set; } = "";
    public string TokenType { get; set; } = "bearer";
}

public class UserOut
{
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "viewer";
    public int? SalesPersonCode { get; set; }
    public bool Active { get; set; } = true;
    /// <summary>CompanyDB user đã chọn lúc đăng nhập.</summary>
    public string CompanyDb { get; set; } = "";
}

/// <summary>Một database SAP B1 được khai báo cho phép truy cập.</summary>
public class DbEntry
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>Danh sách database + database mặc định (dùng ở màn Login).</summary>
public class DbListOut
{
    public List<DbEntry> Databases { get; set; } = new();
    public string Default { get; set; } = "";
}

public class UserCreate
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? FullName { get; set; }
    public string Role { get; set; } = "viewer";
    public int? SalesPersonCode { get; set; }
}

public class UserUpdate
{
    public string? Password { get; set; }
    public string? FullName { get; set; }
    public string? Role { get; set; }
    public int? SalesPersonCode { get; set; }
    public bool? Active { get; set; }
}

public class RoleMatrix
{
    public List<RoleInfo> Roles { get; set; } = new();
    public List<PermissionInfo> Permissions { get; set; } = new();
}

public class RoleInfo
{
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
}

public class PermissionInfo
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public List<string> Roles { get; set; } = new();
}
