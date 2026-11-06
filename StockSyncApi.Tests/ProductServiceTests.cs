using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StockSyncApi.Data;
using StockSyncApi.DTOs;
using StockSyncApi.Models;
using StockSyncApi.Services;
using Xunit;

namespace StockSyncApi.Tests;

public class ProductServiceTests
{
    private static StockSyncDbContext GetInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<StockSyncDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new StockSyncDbContext(options);
    }

    private static ProductService GetService(StockSyncDbContext db)
    {
        return new ProductService(db, NullLogger<ProductService>.Instance);
    }

    [Fact]
    public async Task CreateProduct_ValidDto_ReturnsCreatedProduct()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var dto = new CreateProductDto
        {
            Name = "Coca-Cola 500ml",
            Price = 8.0,
            Quantity = 24,
            Category = "Beverages",
            Barcode = "6001234567890"
        };

        var result = await service.CreateProductAsync(userId, dto);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Coca-Cola 500ml", result.Name);
        Assert.Equal(8.0, result.Price);
        Assert.Equal(24, result.Quantity);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetProducts_ReturnsOnlyActiveProductsForUser()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var service = GetService(db);

        await service.CreateProductAsync(userId, new CreateProductDto { Name = "Product A", Price = 10, Quantity = 5 });
        await service.CreateProductAsync(userId, new CreateProductDto { Name = "Product B", Price = 20, Quantity = 3 });
        await service.CreateProductAsync(otherUserId, new CreateProductDto { Name = "Other User Product", Price = 5, Quantity = 1 });

        var products = await service.GetProductsAsync(userId);

        Assert.Equal(2, products.Count);
        var productNames = products.Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(new List<string> { "Product A", "Product B" }, productNames);
    }

    [Fact]
    public async Task DeleteProduct_SoftDeletesProduct()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Test Product",
            Price = 10,
            Quantity = 5
        });

        var deleted = await service.DeleteProductAsync(userId, product.Id);
        Assert.True(deleted);

        var products = await service.GetProductsAsync(userId);
        Assert.Empty(products);
    }

    [Fact]
    public async Task LogSale_DecrementsProductQuantity()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Test Product",
            Price = 10,
            Quantity = 20
        });

        var saleDto = new SaleDto
        {
            ClientSaleId = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = 3,
            SalePrice = 10,
            PaymentMethod = "CASH",
            Timestamp = DateTime.UtcNow
        };

        var result = await service.LogSaleAsync(userId, saleDto);

        Assert.Equal("CREATED", result.Status);
        Assert.Equal(17, result.ProductQuantityAfter);
    }

    [Fact]
    public async Task LogSale_DuplicateClientSaleId_ReturnsDuplicate()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Test Product",
            Price = 10,
            Quantity = 20
        });

        var clientSaleId = Guid.NewGuid();
        var saleDto = new SaleDto
        {
            ClientSaleId = clientSaleId,
            ProductId = product.Id,
            Quantity = 2,
            SalePrice = 10,
            PaymentMethod = "CASH",
            Timestamp = DateTime.UtcNow
        };

        await service.LogSaleAsync(userId, saleDto);
        var duplicate = await service.LogSaleAsync(userId, saleDto);

        Assert.Equal("DUPLICATE", duplicate.Status);
        Assert.Equal(18, duplicate.ProductQuantityAfter);
    }

    [Fact]
    public async Task AdjustStock_AddsQuantityToProduct()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Test Product",
            Price = 10,
            Quantity = 10
        });

        var adjustmentDto = new StockAdjustmentDto
        {
            ClientAdjustmentId = Guid.NewGuid(),
            ProductId = product.Id,
            QuantityChange = 15,
            Note = "Restock",
            Timestamp = DateTime.UtcNow
        };

        var result = await service.AdjustStockAsync(userId, adjustmentDto);

        Assert.Equal("CREATED", result.Status);
        Assert.Equal(25, result.ProductQuantityAfter);
    }

    [Fact]
    public async Task UpdateSettings_PersistsChanges()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var settingsDto = new SettingsDto
        {
            Language = "zu",
            Currency = "USD",
            LowStockThreshold = 10,
            NotifyLowStock = false,
            NotifyDailySummary = true,
            NotifySyncComplete = true,
            BiometricLock = false
        };

        var result = await service.UpdateSettingsAsync(userId, settingsDto);

        Assert.Equal("zu", result.Language);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(10, result.LowStockThreshold);
        Assert.False(result.NotifyLowStock);
        Assert.True(result.NotifySyncComplete);
        Assert.False(result.BiometricLock);
    }

    [Fact]
    public async Task GetDashboard_ReturnsCorrectSummary()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Test Product",
            Price = 10,
            Quantity = 5
        });

        await service.LogSaleAsync(userId, new SaleDto
        {
            ClientSaleId = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = 2,
            SalePrice = 10,
            PaymentMethod = "CASH",
            Timestamp = DateTime.UtcNow
        });

        var dashboard = await service.GetDashboardAsync(userId);

        Assert.Equal(20, dashboard.TotalSales);
        Assert.Equal(1, dashboard.TransactionCount);
        Assert.Equal(1, dashboard.TopProducts.Count);
        Assert.Equal("Test Product", dashboard.TopProducts[0].Name);
    }

    [Fact]
    public async Task SyncBatch_ProcessesSalesAndAdjustments()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Test Product",
            Price = 10,
            Quantity = 20
        });

        var batch = new SyncBatchDto
        {
            Sales = new List<SaleDto>
            {
                new SaleDto
                {
                    ClientSaleId = Guid.NewGuid(),
                    ProductId = product.Id,
                    Quantity = 2,
                    SalePrice = 10,
                    PaymentMethod = "CASH",
                    Timestamp = DateTime.UtcNow
                }
            },
            Adjustments = new List<StockAdjustmentDto>
            {
                new StockAdjustmentDto
                {
                    ClientAdjustmentId = Guid.NewGuid(),
                    ProductId = product.Id,
                    QuantityChange = 5,
                    Timestamp = DateTime.UtcNow
                }
            }
        };

        var result = await service.SyncBatchAsync(userId, batch);

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, r => Assert.Equal("CREATED", r.Status));
        Assert.NotEmpty(result.ServerProducts);
    }
}
