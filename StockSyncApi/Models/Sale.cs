namespace StockSyncApi.Models;

public class Sale
{
    public Guid Id { get; set; }
    public Guid ClientSaleId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public double SalePrice { get; set; }
    public string PaymentMethod { get; set; } = "CASH";
    public DateTime Timestamp { get; set; }
}
