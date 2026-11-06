using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSyncApi.Services;

namespace StockSyncApi.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IProductService _productService;

    public DashboardController(IProductService productService)
    {
        _productService = productService;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetToday()
    {
        var userId = GetUserId();
        var dashboard = await _productService.GetDashboardAsync(userId);
        return Ok(dashboard);
    }
}
