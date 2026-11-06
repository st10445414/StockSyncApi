namespace StockSyncApi.Models;

public class StockAdjustment
{
    public Guid Id { get; set; }
    public Guid ClientAdjustmentId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int QuantityChange { get; set; }
    public string? Note { get; set; }
    public DateTime Timestamp { get; set; }
}
