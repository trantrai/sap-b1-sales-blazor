namespace SalesWeb.Shared.Dtos;

public class LoginIn
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
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
