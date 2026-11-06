using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StockSyncApi.Data;
using StockSyncApi.Services;
using Xunit;

namespace StockSyncApi.Tests;

public class AuthServiceTests
{
    private static StockSyncDbContext GetInMemoryDb()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<StockSyncDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new StockSyncDbContext(options);
    }

    private static IConfiguration GetConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "TestOnlyKeyForUnitTests_NotForProduction_32chars!",
                ["Jwt:Issuer"] = "StockSync",
                ["Jwt:Audience"] = "StockSyncApp"
            })
            .Build();

    [Fact]
    public async Task Login_NewUser_CreatesUserAndReturnsJwt()
    {
        var db = GetInMemoryDb();
        var config = GetConfig();
        var service = new AuthService(db, config, NullLogger<AuthService>.Instance);

        var result = await service.LoginAsync("test-google-token-123");

        Assert.NotEmpty(result.JwtToken);
        Assert.NotEmpty(result.UserId);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_ExistingUser_ReturnsSameUserId()
    {
        var db = GetInMemoryDb();
        var config = GetConfig();
        var service = new AuthService(db, config, NullLogger<AuthService>.Instance);

        var first = await service.LoginAsync("test-google-token-123");
        var second = await service.LoginAsync("test-google-token-123");

        Assert.Equal(first.UserId, second.UserId);
    }

    [Fact]
    public async Task Login_CreatesDefaultSettings()
    {
        var db = GetInMemoryDb();
        var config = GetConfig();
        var service = new AuthService(db, config, NullLogger<AuthService>.Instance);

        await service.LoginAsync("test-google-token-123");

        var settings = await db.Settings.ToListAsync();
        Assert.Single(settings);
        Assert.Equal("en", settings[0].Language);
        Assert.Equal("ZAR", settings[0].Currency);
        Assert.Equal(5, settings[0].LowStockThreshold);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewJwt()
    {
        var db = GetInMemoryDb();
        var config = GetConfig();
        var service = new AuthService(db, config, NullLogger<AuthService>.Instance);

        await service.LoginAsync("test-google-token-123");
        var result = await service.RefreshAsync("test-google-token-123");

        Assert.NotEmpty(result.JwtToken);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }
}
