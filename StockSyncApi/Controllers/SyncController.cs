using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSyncApi.DTOs;
using StockSyncApi.Services;

namespace StockSyncApi.Controllers;

[ApiController]
[Route("api/sync")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(IProductService productService, ILogger<SyncController> logger)
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

    [HttpPost("batch")]
    public async Task<IActionResult> SyncBatch([FromBody] SyncBatchDto dto)
    {
        var userId = GetUserId();
        var result = await _productService.SyncBatchAsync(userId, dto);
        return Ok(result);
    }
}
