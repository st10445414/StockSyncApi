namespace StockSyncApi.Models;

public class Product
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public int Quantity { get; set; }
    public string? Category { get; set; }
    public string? Barcode { get; set; }
    public string? PhotoUrl { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
    public List<Sale> Sales { get; set; } = new();
    public List<StockAdjustment> Adjustments { get; set; } = new();
}
