using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StockSyncApi.Data;
using StockSyncApi.DTOs;
using StockSyncApi.Services;
using Xunit;

namespace StockSyncApi.Tests;

public class SyncEdgeCaseTests
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
    public async Task SyncBatch_EmptyBatch_ReturnsEmptyResults()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var result = await service.SyncBatchAsync(userId, new SyncBatchDto());

        Assert.Empty(result.Results);
        Assert.Empty(result.ServerProducts);
    }

    [Fact]
    public async Task SyncBatch_DeletedProduct_MarksAsDeleted()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "To Delete", Price = 10, Quantity = 5
        });

        var batch = new SyncBatchDto
        {
            ProductUpdates = new List<ProductUpdateDto>
            {
                new ProductUpdateDto
                {
                    Id = product.Id,
                    Name = "__DELETED__",
                    Price = 0,
                    Quantity = 0,
                    UpdatedAt = DateTime.UtcNow
                }
            }
        };

        var result = await service.SyncBatchAsync(userId, batch);

        Assert.Equal("DELETED", result.Results[0].Status);
        Assert.Empty(result.ServerProducts);
    }

    [Fact]
    public async Task SyncBatch_OfflineCreatedProduct_CreatesOnServer()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var offlineProductId = Guid.NewGuid();
        var batch = new SyncBatchDto
        {
            ProductUpdates = new List<ProductUpdateDto>
            {
                new ProductUpdateDto
                {
                    Id = offlineProductId,
                    Name = "Offline Product",
                    Price = 15,
                    Quantity = 10,
                    UpdatedAt = DateTime.UtcNow
                }
            }
        };

        var result = await service.SyncBatchAsync(userId, batch);

        Assert.Equal("CREATED", result.Results[0].Status);
        Assert.Single(result.ServerProducts);
        Assert.Equal("Offline Product", result.ServerProducts[0].Name);
    }

    [Fact]
    public async Task SyncBatch_SaleOnNonexistentProduct_ReturnsFailed()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var batch = new SyncBatchDto
        {
            Sales = new List<SaleDto>
            {
                new SaleDto
                {
                    ClientSaleId = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    Quantity = 1,
                    SalePrice = 10,
                    PaymentMethod = "CASH",
                    Timestamp = DateTime.UtcNow
                }
            }
        };

        var result = await service.SyncBatchAsync(userId, batch);

        Assert.Equal("FAILED", result.Results[0].Status);
    }

    [Fact]
    public async Task SyncBatch_DuplicateSaleInBatch_ReturnsDuplicate()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Test", Price = 10, Quantity = 20
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

        var batch = new SyncBatchDto
        {
            Sales = new List<SaleDto> { saleDto, saleDto with { } }
        };

        var result = await service.SyncBatchAsync(userId, batch);

        Assert.Equal(2, result.Results.Count);
        Assert.Equal("CREATED", result.Results[0].Status);
        Assert.Equal("DUPLICATE", result.Results[1].Status);
    }

    [Fact]
    public async Task SyncBatch_AdjustmentClampsToZero()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Test", Price = 10, Quantity = 3
        });

        var batch = new SyncBatchDto
        {
            Adjustments = new List<StockAdjustmentDto>
            {
                new StockAdjustmentDto
                {
                    ClientAdjustmentId = Guid.NewGuid(),
                    ProductId = product.Id,
                    QuantityChange = -10,
                    Timestamp = DateTime.UtcNow
                }
            }
        };

        var result = await service.SyncBatchAsync(userId, batch);

        Assert.Equal("CREATED", result.Results[0].Status);
        Assert.Equal(0, result.ServerProducts[0].Quantity);
    }

    [Fact]
    public async Task SyncBatch_ConflictResolution_OlderClientUpdateIgnored()
    {
        var db = GetInMemoryDb();
        var userId = Guid.NewGuid();
        var service = GetService(db);

        var product = await service.CreateProductAsync(userId, new CreateProductDto
        {
            Name = "Original", Price = 10, Quantity = 5
        });

        var olderUpdate = new ProductUpdateDto
        {
            Id = product.Id,
            Name = "Should Be Ignored",
            Price = 99,
            Quantity = 100,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var batch = new SyncBatchDto { ProductUpdates = new List<ProductUpdateDto> { olderUpdate } };
        var result = await service.SyncBatchAsync(userId, batch);

        Assert.Equal("UPDATED", result.Results[0].Status);
        Assert.Equal("Original", result.ServerProducts[0].Name);
        Assert.Equal(10, result.ServerProducts[0].Price);
    }
}
