namespace jokessvc.Models;

public class RefreshRequest
{
    public int BatchSize { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}
