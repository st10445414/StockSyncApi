using Microsoft.EntityFrameworkCore;
using StockSyncApi.Data;
using StockSyncApi.DTOs;
using StockSyncApi.Models;

namespace StockSyncApi.Services;

public interface IProductService
{
    Task<List<ProductDto>> GetProductsAsync(Guid userId);
    Task<ProductDto?> GetProductAsync(Guid userId, Guid productId);
    Task<ProductDto> CreateProductAsync(Guid userId, CreateProductDto dto);
    Task<ProductDto?> UpdateProductAsync(Guid userId, Guid productId, UpdateProductDto dto);
    Task<bool> DeleteProductAsync(Guid userId, Guid productId);
    Task<SaleResponseDto> LogSaleAsync(Guid userId, SaleDto dto);
    Task<StockAdjustmentResponseDto> AdjustStockAsync(Guid userId, StockAdjustmentDto dto);
    Task<SyncResultDto> SyncBatchAsync(Guid userId, SyncBatchDto dto);
    Task<DashboardDto> GetDashboardAsync(Guid userId);
    Task<SettingsDto> GetSettingsAsync(Guid userId);
    Task<SettingsDto> UpdateSettingsAsync(Guid userId, SettingsDto dto);
    Task<bool> RegisterDeviceAsync(Guid userId, string fcmToken);
}

public class ProductService : IProductService
{
    private readonly StockSyncDbContext _db;
    private readonly ILogger<ProductService> _logger;

    public ProductService(StockSyncDbContext db, ILogger<ProductService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<ProductDto>> GetProductsAsync(Guid userId)
    {
        _logger.LogInformation("Fetching products for user {UserId}", userId);
        return await _db.Products
            .Where(p => p.UserId == userId && p.IsActive)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<ProductDto?> GetProductAsync(Guid userId, Guid productId)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.UserId == userId && p.IsActive);
        return product == null ? null : MapToDto(product);
    }

    public async Task<ProductDto> CreateProductAsync(Guid userId, CreateProductDto dto)
    {
        _logger.LogInformation("Creating product {Name} for user {UserId}", dto.Name, userId);
        var product = new Product
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            Price = dto.Price,
            Quantity = dto.Quantity,
            Category = dto.Category,
            Barcode = dto.Barcode,
            LowStockThreshold = 5,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Product {ProductId} created", product.Id);
        return MapToDto(product);
    }

    public async Task<ProductDto?> UpdateProductAsync(Guid userId, Guid productId, UpdateProductDto dto)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.UserId == userId && p.IsActive);
        if (product == null) return null;

        product.Name = dto.Name;
        product.Price = dto.Price;
        product.Category = dto.Category;
        product.Barcode = dto.Barcode;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Product {ProductId} updated", productId);
        return MapToDto(product);
    }

    public async Task<bool> DeleteProductAsync(Guid userId, Guid productId)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.UserId == userId && p.IsActive);
        if (product == null) return false;

        // Soft delete to preserve historical sales data
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Product {ProductId} soft-deleted", productId);
        return true;
    }

    public async Task<SaleResponseDto> LogSaleAsync(Guid userId, SaleDto dto)
    {
        // Idempotency: check if this client sale already exists
        var existing = await _db.Sales.FirstOrDefaultAsync(s => s.ClientSaleId == dto.ClientSaleId && s.UserId == userId);
        if (existing != null)
        {
            _logger.LogInformation("Duplicate sale detected for ClientSaleId {ClientSaleId}", dto.ClientSaleId);
            var existingProduct = await _db.Products.FindAsync(existing.ProductId);
            return new SaleResponseDto
            {
                ServerSaleId = existing.Id,
                ClientSaleId = existing.ClientSaleId,
                Status = "DUPLICATE",
                ProductQuantityAfter = existingProduct?.Quantity ?? 0
            };
        }

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.UserId == userId && p.IsActive);
        if (product == null)
            throw new ArgumentException("Product not found");

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            ClientSaleId = dto.ClientSaleId,
            UserId = userId,
            ProductId = dto.ProductId,
            Quantity = dto.Quantity,
            SalePrice = dto.SalePrice,
            PaymentMethod = dto.PaymentMethod,
            Timestamp = dto.Timestamp
        };
        _db.Sales.Add(sale);

        // Decrement stock using last-write-wins
        product.Quantity = Math.Max(0, product.Quantity - dto.Quantity);
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Sale logged for product {ProductId}, new qty {Qty}", product.Id, product.Quantity);

        return new SaleResponseDto
        {
            ServerSaleId = sale.Id,
            ClientSaleId = sale.ClientSaleId,
            Status = "CREATED",
            ProductQuantityAfter = product.Quantity
        };
    }

    public async Task<StockAdjustmentResponseDto> AdjustStockAsync(Guid userId, StockAdjustmentDto dto)
    {
        var existing = await _db.StockAdjustments
            .FirstOrDefaultAsync(a => a.ClientAdjustmentId == dto.ClientAdjustmentId && a.UserId == userId);
        if (existing != null)
        {
            _logger.LogInformation("Duplicate adjustment detected for ClientAdjustmentId {Id}", dto.ClientAdjustmentId);
            var existingProduct = await _db.Products.FindAsync(existing.ProductId);
            return new StockAdjustmentResponseDto
            {
                ServerAdjustmentId = existing.Id,
                ClientAdjustmentId = existing.ClientAdjustmentId,
                Status = "DUPLICATE",
                ProductQuantityAfter = existingProduct?.Quantity ?? 0
            };
        }

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.UserId == userId && p.IsActive);
        if (product == null)
            throw new ArgumentException("Product not found");

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            ClientAdjustmentId = dto.ClientAdjustmentId,
            UserId = userId,
            ProductId = dto.ProductId,
            QuantityChange = dto.QuantityChange,
            Note = dto.Note,
            Timestamp = dto.Timestamp
        };
        _db.StockAdjustments.Add(adjustment);

        product.Quantity = Math.Max(0, product.Quantity + dto.QuantityChange);
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Stock adjusted for product {ProductId}, new qty {Qty}", product.Id, product.Quantity);

        return new StockAdjustmentResponseDto
        {
            ServerAdjustmentId = adjustment.Id,
            ClientAdjustmentId = adjustment.ClientAdjustmentId,
            Status = "CREATED",
            ProductQuantityAfter = product.Quantity
        };
    }

    public async Task<SyncResultDto> SyncBatchAsync(Guid userId, SyncBatchDto dto)
    {
        _logger.LogInformation("Sync batch received: {SaleCount} sales, {AdjCount} adjustments, {ProdCount} product updates",
            dto.Sales.Count, dto.Adjustments.Count, dto.ProductUpdates.Count);

        var result = new SyncResultDto();

        foreach (var sale in dto.Sales)
        {
            try
            {
                var saleResult = await LogSaleAsync(userId, sale);
                result.Results.Add(new SyncResultItemDto
                {
                    ClientRecordId = sale.ClientSaleId.ToString(),
                    Status = saleResult.Status,
                    ServerId = saleResult.ServerSaleId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync sale {ClientSaleId}", sale.ClientSaleId);
                result.Results.Add(new SyncResultItemDto
                {
                    ClientRecordId = sale.ClientSaleId.ToString(),
                    Status = "FAILED"
                });
            }
        }

        foreach (var adj in dto.Adjustments)
        {
            try
            {
                var adjResult = await AdjustStockAsync(userId, adj);
                result.Results.Add(new SyncResultItemDto
                {
                    ClientRecordId = adj.ClientAdjustmentId.ToString(),
                    Status = adjResult.Status,
                    ServerId = adjResult.ServerAdjustmentId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync adjustment {Id}", adj.ClientAdjustmentId);
                result.Results.Add(new SyncResultItemDto
                {
                    ClientRecordId = adj.ClientAdjustmentId.ToString(),
                    Status = "FAILED"
                });
            }
        }

        foreach (var prod in dto.ProductUpdates)
        {
            try
            {
                if (prod.Name == "__DELETED__")
                {
                    var toDelete = await _db.Products
                        .FirstOrDefaultAsync(p => p.Id == prod.Id && p.UserId == userId);
                    if (toDelete != null)
                    {
                        toDelete.IsActive = false;
                        toDelete.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync();
                        result.Results.Add(new SyncResultItemDto
                        {
                            ClientRecordId = prod.Id.ToString(),
                            Status = "DELETED"
                        });
                    }
                    else
                    {
                        result.Results.Add(new SyncResultItemDto
                        {
                            ClientRecordId = prod.Id.ToString(),
                            Status = "NOT_FOUND"
                        });
                    }
                    continue;
                }

                var product = await _db.Products
                    .FirstOrDefaultAsync(p => p.Id == prod.Id && p.UserId == userId);
                if (product != null)
                {
                    if (prod.UpdatedAt > product.UpdatedAt)
                    {
                        product.Name = prod.Name;
                        product.Price = prod.Price;
                        product.Quantity = prod.Quantity;
                        product.Category = prod.Category;
                        product.Barcode = prod.Barcode;
                        product.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync();
                    }
                    result.Results.Add(new SyncResultItemDto
                    {
                        ClientRecordId = prod.Id.ToString(),
                        Status = "UPDATED"
                    });
                }
                else
                {
                    // Product created offline — create it on the server
                    var newProduct = new Product
                    {
                        Id = prod.Id,
                        UserId = userId,
                        Name = prod.Name,
                        Price = prod.Price,
                        Quantity = prod.Quantity,
                        Category = prod.Category,
                        Barcode = prod.Barcode,
                        LowStockThreshold = 5,
                        IsActive = true,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.Products.Add(newProduct);
                    await _db.SaveChangesAsync();
                    result.Results.Add(new SyncResultItemDto
                    {
                        ClientRecordId = prod.Id.ToString(),
                        Status = "CREATED"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync product {Id}", prod.Id);
                result.Results.Add(new SyncResultItemDto
                {
                    ClientRecordId = prod.Id.ToString(),
                    Status = "FAILED"
                });
            }
        }

        // Return current server-side product state for reconciliation
        result.ServerProducts = await GetProductsAsync(userId);
        _logger.LogInformation("Sync batch complete with {Count} results", result.Results.Count);
        return result;
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid userId)
    {
        var today = DateTime.UtcNow.Date;
        var sales = await _db.Sales
            .Where(s => s.UserId == userId && s.Timestamp.Date == today)
            .ToListAsync();

        var settings = await _db.Settings.FirstOrDefaultAsync(s => s.UserId == userId);
        var threshold = settings?.LowStockThreshold ?? 5;

        var lowStockCount = await _db.Products
            .Where(p => p.UserId == userId && p.IsActive && p.Quantity < threshold)
            .CountAsync();

        var topProductIds = sales
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, QuantitySold = g.Sum(s => s.Quantity) })
            .OrderByDescending(x => x.QuantitySold)
            .Take(3)
            .ToList();

        var topProductDtos = new List<TopProductDto>();
        foreach (var tp in topProductIds)
        {
            var product = await _db.Products.FindAsync(tp.ProductId);
            if (product != null)
            {
                topProductDtos.Add(new TopProductDto
                {
                    Name = product.Name,
                    QuantitySold = tp.QuantitySold
                });
            }
        }

        return new DashboardDto
        {
            TotalSales = sales.Sum(s => s.SalePrice * s.Quantity),
            TransactionCount = sales.Count,
            LowStockCount = lowStockCount,
            TopProducts = topProductDtos
        };
    }

    public async Task<SettingsDto> GetSettingsAsync(Guid userId)
    {
        var settings = await _db.Settings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings == null)
        {
            settings = new Settings { UserId = userId };
            _db.Settings.Add(settings);
            await _db.SaveChangesAsync();
        }
        return MapSettingsToDto(settings);
    }

    public async Task<SettingsDto> UpdateSettingsAsync(Guid userId, SettingsDto dto)
    {
        var settings = await _db.Settings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings == null)
        {
            settings = new Settings { UserId = userId };
            _db.Settings.Add(settings);
        }

        settings.Language = dto.Language;
        settings.Currency = dto.Currency;
        settings.LowStockThreshold = dto.LowStockThreshold;
        settings.NotifyLowStock = dto.NotifyLowStock;
        settings.NotifyDailySummary = dto.NotifyDailySummary;
        settings.NotifySyncComplete = dto.NotifySyncComplete;
        settings.BiometricLock = dto.BiometricLock;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Settings updated for user {UserId}", userId);
        return MapSettingsToDto(settings);
    }

    public async Task<bool> RegisterDeviceAsync(Guid userId, string fcmToken)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;

        user.FcmToken = fcmToken;
        await _db.SaveChangesAsync();
        _logger.LogInformation("FCM token registered for user {UserId}", userId);
        return true;
    }

    private static ProductDto MapToDto(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Price = p.Price,
        Quantity = p.Quantity,
        Category = p.Category,
        Barcode = p.Barcode,
        PhotoUrl = p.PhotoUrl,
        LowStockThreshold = p.LowStockThreshold,
        IsActive = p.IsActive,
        UpdatedAt = p.UpdatedAt
    };

    private static SettingsDto MapSettingsToDto(Settings s) => new()
    {
        Language = s.Language,
        Currency = s.Currency,
        LowStockThreshold = s.LowStockThreshold,
        NotifyLowStock = s.NotifyLowStock,
        NotifyDailySummary = s.NotifyDailySummary,
        NotifySyncComplete = s.NotifySyncComplete,
        BiometricLock = s.BiometricLock
    };
}
