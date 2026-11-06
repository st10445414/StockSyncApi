namespace StockSyncApi.Models;

public class Settings
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Language { get; set; } = "en";
    public string Currency { get; set; } = "ZAR";
    public int LowStockThreshold { get; set; } = 5;
    public bool NotifyLowStock { get; set; } = true;
    public bool NotifyDailySummary { get; set; } = true;
    public bool NotifySyncComplete { get; set; } = false;
    public bool BiometricLock { get; set; } = true;
}
