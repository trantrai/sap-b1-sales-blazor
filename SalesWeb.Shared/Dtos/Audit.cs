namespace SalesWeb.Shared.Dtos;

public class AuditOut
{
    public int Id { get; set; }
    public string CreatedAt { get; set; } = "";
    public string Operation { get; set; } = "";
    public string OperationLabel { get; set; } = "";
    public string EntityKey { get; set; } = "";
    public string Username { get; set; } = "";
    public bool Success { get; set; }
    public int? HttpStatus { get; set; }
    public string Error { get; set; } = "";
    public int DurationMs { get; set; }
    public int RetryCount { get; set; }
    public int? RetriedFrom { get; set; }
    public bool Resolved { get; set; }
    public string Note { get; set; } = "";
    public bool RetriedOk { get; set; }
}

public class AuditDetailOut : AuditOut
{
    public System.Text.Json.JsonElement RequestJson { get; set; }
    public System.Text.Json.JsonElement ResponseJson { get; set; }
}

public class PagedAudit
{
    public int Total { get; set; }
    public List<AuditOut> Items { get; set; } = new();
}

public class FailedCountOut
{
    public int Failed { get; set; }
}

public class RetryOut
{
    public bool Ok { get; set; }
    public string Operation { get; set; } = "";
    public string EntityKey { get; set; } = "";
    public Dictionary<string, object?>? Detail { get; set; }
}

public class RetryAllOut
{
    public int Retried { get; set; }
    public List<Dictionary<string, object?>> Ok { get; set; } = new();
    public List<Dictionary<string, object?>> Fail { get; set; } = new();
}
