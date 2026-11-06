using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockSyncApi.Data;
using StockSyncApi.DTOs;
using Xunit;

namespace StockSyncApi.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var dbContextOptions = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<StockSyncDbContext>));
                if (dbContextOptions != null)
                    services.Remove(dbContextOptions);

                services.AddDbContext<StockSyncDbContext>(options =>
                    options.UseInMemoryDatabase("IntegrationTestDb"));
            });
        });
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    private async Task<string> GetValidJwtTokenAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            GoogleIdToken = "integration-test-token"
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return result!.JwtToken;
    }

    [Fact]
    public async Task Login_ValidToken_ReturnsJwt()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            GoogleIdToken = "integration-test-login"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotEmpty(result!.JwtToken);
        Assert.NotEmpty(result.UserId);
    }

    [Fact]
    public async Task Login_EmptyToken_ReturnsBadRequest()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            GoogleIdToken = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Products_WithoutAuth_ReturnsUnauthorized()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Products_WithAuth_ReturnsOk()
    {
        var token = await GetValidJwtTokenAsync();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithAuth_ReturnsCreated()
    {
        var token = await GetValidJwtTokenAsync();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/products", new CreateProductDto
        {
            Name = "Integration Test Product",
            Price = 12.5,
            Quantity = 10
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal("Integration Test Product", product!.Name);
    }

    [Fact]
    public async Task CreateProduct_EmptyName_ReturnsBadRequest()
    {
        var token = await GetValidJwtTokenAsync();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/products", new CreateProductDto
        {
            Name = "",
            Price = 10,
            Quantity = 5
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_WithAuth_ReturnsOk()
    {
        var token = await GetValidJwtTokenAsync();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/dashboard/today");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardDto>();
        Assert.Equal(0, dashboard!.TotalSales);
    }

    [Fact]
    public async Task Settings_WithAuth_ReturnsOk()
    {
        var token = await GetValidJwtTokenAsync();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await response.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.Equal("en", settings!.Language);
    }

    [Fact]
    public async Task SyncBatch_WithAuth_EmptyBatch_ReturnsOk()
    {
        var token = await GetValidJwtTokenAsync();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/sync/batch", new SyncBatchDto());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SyncResultDto>();
        Assert.Empty(result!.Results);
    }

    [Fact]
    public async Task GetProduct_NonexistentId_ReturnsNotFound()
    {
        var token = await GetValidJwtTokenAsync();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
