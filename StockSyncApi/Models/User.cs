namespace StockSyncApi.Models;

public class User
{
    public Guid Id { get; set; }
    public string GoogleId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FcmToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public Settings? Settings { get; set; }
    public List<Product> Products { get; set; } = new();
    public List<Sale> Sales { get; set; } = new();
    public List<StockAdjustment> StockAdjustments { get; set; } = new();
}
