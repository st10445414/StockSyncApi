using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSyncApi.DTOs;
using StockSyncApi.Services;

namespace StockSyncApi.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IProductService _productService;

    public DevicesController(IProductService productService)
    {
        _productService = productService;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] DeviceRegistrationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FcmToken))
            return BadRequest(new { error = "FCM token is required" });

        var userId = GetUserId();
        var success = await _productService.RegisterDeviceAsync(userId, dto.FcmToken);
        return success ? Ok(new { success = true }) : NotFound();
    }
}
