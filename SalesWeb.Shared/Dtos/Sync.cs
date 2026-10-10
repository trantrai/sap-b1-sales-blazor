namespace SalesWeb.Shared.Dtos;

public class SyncStatusOut
{
    public bool Running { get; set; }
    public string? StartedAt { get; set; }
    public string? FinishedAt { get; set; }
    public string? Error { get; set; }
    public List<SyncTableOut> Tables { get; set; } = new();
}

public class SyncTableOut
{
    public string Table { get; set; } = "";
    public int Count { get; set; }
    public string? LastSync { get; set; }
    public int LastRecords { get; set; }
}

public class SyncStartOut
{
    public bool Started { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>Trạng thái đồng bộ của một entity (khớp GET /api/sync/status của backend).</summary>
public class SyncEntityStatus
{
    public string Entity { get; set; } = "";
    public string Label { get; set; } = "";
    public int Count { get; set; }
    public bool Running { get; set; }
    public string? LastStatus { get; set; }
    public string? LastStartedAt { get; set; }
    public string? LastFinishedAt { get; set; }
    public int LastRecords { get; set; }
    public string LastError { get; set; } = "";
}

public class HealthOut
{
    public string Status { get; set; } = "ok";
    public string B1Mode { get; set; } = "mock";
    public string? B1Url { get; set; }
}
