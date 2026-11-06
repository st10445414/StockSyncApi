using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StockSyncApi.Data;
using StockSyncApi.DTOs;
using StockSyncApi.Services;
using Xunit;

namespace StockSyncApi.Tests;

public class ProductServiceEdgeCaseTests
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
    public async Task UpdateProduct_ValidDto_UpdatesFields()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Original", Price = 10, Quantity = 5
        });

        var updated = await service.UpdateProductAsync(userId, product.Id, new UpdateProductDto
        {
            Name = "Updated Name", Price = 15, Category = "Drinks", Barcode = "123"
        });

        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal(15, updated.Price);
        Assert.Equal("Drinks", updated.Category);
    }

    [Fact]
    public async Task UpdateProduct_CrossUser_ReturnsNull()
    {
        var db = GetInMemoryDb();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userA, new CreateProductDto
        {
            Name = "User A Product", Price = 10, Quantity = 5
        });

        var result = await service.UpdateProductAsync(userB, product.Id, new UpdateProductDto
        {
            Name = "Hacked", Price = 1
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteProduct_CrossUser_ReturnsFalse()
    {
        var db = GetInMemoryDb();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userA, new CreateProductDto
        {
            Name = "User A Product", Price = 10, Quantity = 5
        });

        var deleted = await service.DeleteProductAsync(userB, product.Id);

        Assert.False(deleted);
        var products = await service.GetProductsAsync(userA);
        Assert.Single(products);
    }

    [Fact]
    public async Task GetProduct_SingleProduct_ReturnsCorrectProduct()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Single Item", Price = 25, Quantity = 3
        });

        var fetched = await service.GetProductAsync(userId, product.Id);

        Assert.NotNull(fetched);
        Assert.Equal("Single Item", fetched!.Name);
        Assert.Equal(25, fetched.Price);
    }

    [Fact]
    public async Task GetProduct_NonexistentId_ReturnsNull()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var result = await service.GetProductAsync(userId, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task LogSale_Oversell_ClampsToZero()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Limited", Price = 10, Quantity = 2
        });

        var result = await service.LogSaleAsync(userId, new SaleDto
        {
            ClientSaleId = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = 10,
            SalePrice = 10,
            PaymentMethod = "CASH",
            Timestamp = DateTime.UtcNow
        });

        Assert.Equal(0, result.ProductQuantityAfter);
    }

    [Fact]
    public async Task LogSale_OnOtherUserProduct_ThrowsArgumentException()
    {
        var db = GetInMemoryDb();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userA, new CreateProductDto
        {
            Name = "User A Product", Price = 10, Quantity = 5
        });

        await Assert.ThrowsAsync<ArgumentException>(() => service.LogSaleAsync(userB, new SaleDto
        {
            ClientSaleId = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = 1,
            SalePrice = 10,
            PaymentMethod = "CASH",
            Timestamp = DateTime.UtcNow
        }));
    }

    [Fact]
    public async Task AdjustStock_DuplicateId_ReturnsDuplicate()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Test", Price = 10, Quantity = 10
        });

        var adjId = Guid.NewGuid();
        var dto = new StockAdjustmentDto
        {
            ClientAdjustmentId = adjId,
            ProductId = product.Id,
            QuantityChange = 5,
            Timestamp = DateTime.UtcNow
        };

        await service.AdjustStockAsync(userId, dto);
        var dup = await service.AdjustStockAsync(userId, dto);

        Assert.Equal("DUPLICATE", dup.Status);
        Assert.Equal(15, dup.ProductQuantityAfter);
    }

    [Fact]
    public async Task GetDashboard_NoSales_ReturnsZeros()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Product", Price = 10, Quantity = 5
        });

        var dashboard = await service.GetDashboardAsync(userId);

        Assert.Equal(0, dashboard.TotalSales);
        Assert.Equal(0, dashboard.TransactionCount);
        Assert.Empty(dashboard.TopProducts);
    }
}
