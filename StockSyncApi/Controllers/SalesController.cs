using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSyncApi.DTOs;
using StockSyncApi.Services;

namespace StockSyncApi.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<SalesController> _logger;

    public SalesController(IProductService productService, ILogger<SalesController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }

    [HttpPost]
    public async Task<IActionResult> LogSale([FromBody] SaleDto dto)
    {
        if (dto.Quantity <= 0)
            return BadRequest(new { error = "Quantity must be > 0" });
        if (dto.SalePrice < 0)
            return BadRequest(new { error = "Sale price must be >= 0" });
        if (dto.ClientSaleId == Guid.Empty)
            return BadRequest(new { error = "ClientSaleId is required" });

        var userId = GetUserId();
        try
        {
            var result = await _productService.LogSaleAsync(userId, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
