using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSyncApi.DTOs;
using StockSyncApi.Services;

namespace StockSyncApi.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IProductService _productService;

    public SettingsController(IProductService productService)
    {
        _productService = productService;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = GetUserId();
        var settings = await _productService.GetSettingsAsync(userId);
        return Ok(settings);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] SettingsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Language))
            return BadRequest(new { error = "Language is required" });
        if (dto.LowStockThreshold < 0)
            return BadRequest(new { error = "Low-stock threshold must be >= 0" });

        var userId = GetUserId();
        var settings = await _productService.UpdateSettingsAsync(userId, dto);
        return Ok(settings);
    }
}
