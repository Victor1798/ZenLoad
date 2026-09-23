namespace ZenLoad.Models;

public enum ActivityStatus
{
    Moved,
    Ignored,
    Error,
    Info
}

public sealed class ActivityEntry
{
    public ActivityEntry(DateTime timestamp, string fileName, ActivityStatus status, string details)
    {
        Timestamp = timestamp;
        FileName = fileName;
        Status = status;
        Details = details;
    }

    public DateTime Timestamp { get; }
    public string FileName { get; }
    public ActivityStatus Status { get; }
    public string Details { get; }
    public string TimeText => Timestamp.ToString("HH:mm:ss");
    public string StatusText => Status switch
    {
        ActivityStatus.Moved => "Movido",
        ActivityStatus.Ignored => "Ignorado",
        ActivityStatus.Error => "Error",
        _ => "Información"
    };
}
