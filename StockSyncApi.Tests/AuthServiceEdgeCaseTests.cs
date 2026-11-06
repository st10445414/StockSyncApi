using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StockSyncApi.Data;
using StockSyncApi.Services;
using Xunit;

namespace StockSyncApi.Tests;

public class AuthServiceEdgeCaseTests
{
    private static StockSyncDbContext GetInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<StockSyncDbContext>()
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
    public async Task Login_EmptyToken_ThrowsArgumentException()
    {
        var db = GetInMemoryDb();
        var service = new AuthService(db, GetConfig(), NullLogger<AuthService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => service.LoginAsync(""));
    }

    [Fact]
    public async Task Login_NullToken_ThrowsArgumentException()
    {
        var db = GetInMemoryDb();
        var service = new AuthService(db, GetConfig(), NullLogger<AuthService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => service.LoginAsync(null!));
    }

    [Fact]
    public async Task Login_DifferentTokens_CreateDifferentUsers()
    {
        var db = GetInMemoryDb();
        var service = new AuthService(db, GetConfig(), NullLogger<AuthService>.Instance);

        var result1 = await service.LoginAsync("token-alpha");
        var result2 = await service.LoginAsync("token-beta");

        Assert.NotEqual(result1.UserId, result2.UserId);
    }

    [Fact]
    public async Task Refresh_UnknownUser_ThrowsUnauthorizedAccess()
    {
        var db = GetInMemoryDb();
        var service = new AuthService(db, GetConfig(), NullLogger<AuthService>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync("nonexistent-token"));
    }

    [Fact]
    public async Task Login_SameTokenDifferentTimestamps_CreatesSingleUser()
    {
        var db = GetInMemoryDb();
        var service = new AuthService(db, GetConfig(), NullLogger<AuthService>.Instance);

        var t1 = await service.LoginAsync("stable-token");
        var t2 = await service.LoginAsync("stable-token");

        Assert.Equal(t1.UserId, t2.UserId);
        Assert.Single(db.Users.ToList());
    }

    [Fact]
    public void GenerateJwt_MissingKey_ThrowsInvalidOperationException()
    {
        var db = GetInMemoryDb();
        var emptyConfig = new ConfigurationBuilder().Build();
        var service = new AuthService(db, emptyConfig, NullLogger<AuthService>.Instance);

        Assert.Throws<InvalidOperationException>(() => service.LoginAsync("any-token").GetAwaiter().GetResult());
    }
}
