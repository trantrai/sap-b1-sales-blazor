namespace SalesWeb.Shared.Dtos;

public class ConnSettingsIn
{
    public bool B1Mock { get; set; } = true;
    public string B1ServiceLayerUrl { get; set; } = "";
    public string B1CompanyDb { get; set; } = "";
    public string B1Username { get; set; } = "";
    public string B1Password { get; set; } = ""; // rỗng = giữ nguyên
    public bool B1VerifySsl { get; set; } = true;
    public double B1Timeout { get; set; } = 30;
}

public class ConnTestIn
{
    public bool? B1Mock { get; set; }
    public string? B1ServiceLayerUrl { get; set; }
    public string? B1CompanyDb { get; set; }
    public string? B1Username { get; set; }
    public string? B1Password { get; set; }
    public bool? B1VerifySsl { get; set; }
    public double Timeout { get; set; } = 10;
}

public class ConnSettingsOut
{
    public bool B1Mock { get; set; }
    public string B1ServiceLayerUrl { get; set; } = "";
    public string B1CompanyDb { get; set; } = "";
    public string B1Username { get; set; } = "";
    public string B1Password { get; set; } = ""; // "********" nếu đã có
    public bool B1VerifySsl { get; set; }
    public double B1Timeout { get; set; }
    public bool FromDb { get; set; }
}

public class ConnTestOut
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
}

public class ConnSaveOut
{
    public bool Ok { get; set; }
    public string Mode { get; set; } = "mock";
    public string? Hint { get; set; }
}
