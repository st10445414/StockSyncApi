namespace StockSyncApi.DTOs;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public int Quantity { get; set; }
    public string? Category { get; set; }
    public string? Barcode { get; set; }
    public string? PhotoUrl { get; set; }
    public int LowStockThreshold { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public int Quantity { get; set; }
    public string? Category { get; set; }
    public string? Barcode { get; set; }
    public string? PhotoBase64 { get; set; }
}

public class UpdateProductDto
{
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? Category { get; set; }
    public string? Barcode { get; set; }
    public string? PhotoBase64 { get; set; }
}

public class SaleDto
{
    public Guid ClientSaleId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public double SalePrice { get; set; }
    public string PaymentMethod { get; set; } = "CASH";
    public DateTime Timestamp { get; set; }
}

public class SaleResponseDto
{
    public Guid ServerSaleId { get; set; }
    public Guid ClientSaleId { get; set; }
    public string Status { get; set; } = "CREATED";
    public int ProductQuantityAfter { get; set; }
}

public class StockAdjustmentDto
{
    public Guid ClientAdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityChange { get; set; }
    public string? Note { get; set; }
    public DateTime Timestamp { get; set; }
}

public class StockAdjustmentResponseDto
{
    public Guid ServerAdjustmentId { get; set; }
    public Guid ClientAdjustmentId { get; set; }
    public string Status { get; set; } = "CREATED";
    public int ProductQuantityAfter { get; set; }
}

public class SyncBatchDto
{
    public List<SaleDto> Sales { get; set; } = new();
    public List<StockAdjustmentDto> Adjustments { get; set; } = new();
    public List<ProductUpdateDto> ProductUpdates { get; set; } = new();
}

public class ProductUpdateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public int Quantity { get; set; }
    public string? Category { get; set; }
    public string? Barcode { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncResultDto
{
    public List<SyncResultItemDto> Results { get; set; } = new();
    public List<ProductDto> ServerProducts { get; set; } = new();
}

public class SyncResultItemDto
{
    public string ClientRecordId { get; set; } = string.Empty;
    public string Status { get; set; } = "CREATED";
    public Guid? ServerId { get; set; }
}

public class DashboardDto
{
    public double TotalSales { get; set; }
    public int TransactionCount { get; set; }
    public int LowStockCount { get; set; }
    public List<TopProductDto> TopProducts { get; set; } = new();
}

public class TopProductDto
{
    public string Name { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
}

public class SettingsDto
{
    public string Language { get; set; } = "en";
    public string Currency { get; set; } = "ZAR";
    public int LowStockThreshold { get; set; } = 5;
    public bool NotifyLowStock { get; set; } = true;
    public bool NotifyDailySummary { get; set; } = true;
    public bool NotifySyncComplete { get; set; } = false;
    public bool BiometricLock { get; set; } = true;
}

public class DeviceRegistrationDto
{
    public string FcmToken { get; set; } = string.Empty;
}
