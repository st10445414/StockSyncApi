using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSyncApi.DTOs;
using StockSyncApi.Services;

namespace StockSyncApi.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductService productService, ILogger<ProductsController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdClaim!.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        var products = await _productService.GetProductsAsync(userId);
        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetUserId();
        var product = await _productService.GetProductAsync(userId, id);
        return product == null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Product name is required" });
        if (dto.Price < 0)
            return BadRequest(new { error = "Price must be >= 0" });

        var userId = GetUserId();
        var product = await _productService.CreateProductAsync(userId, dto);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Product name is required" });
        if (dto.Price < 0)
            return BadRequest(new { error = "Price must be >= 0" });

        var userId = GetUserId();
        var product = await _productService.UpdateProductAsync(userId, id, dto);
        return product == null ? NotFound() : Ok(product);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var deleted = await _productService.DeleteProductAsync(userId, id);
        return deleted ? NoContent() : NotFound();
    }
}
