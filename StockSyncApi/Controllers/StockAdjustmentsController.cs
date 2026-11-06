using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSyncApi.DTOs;
using StockSyncApi.Services;

namespace StockSyncApi.Controllers;

[ApiController]
[Route("api/stock-adjustments")]
[Authorize]
public class StockAdjustmentsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<StockAdjustmentsController> _logger;

    public StockAdjustmentsController(IProductService productService, ILogger<StockAdjustmentsController> logger)
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
    public async Task<IActionResult> AdjustStock([FromBody] StockAdjustmentDto dto)
    {
        if (dto.ClientAdjustmentId == Guid.Empty)
            return BadRequest(new { error = "ClientAdjustmentId is required" });
        if (dto.QuantityChange == 0)
            return BadRequest(new { error = "QuantityChange must be non-zero" });

        var userId = GetUserId();
        try
        {
            var result = await _productService.AdjustStockAsync(userId, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
