using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSyncApi.DTOs;
using StockSyncApi.Services;

namespace StockSyncApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GoogleIdToken))
        {
            _logger.LogWarning("Login attempted without Google ID token");
            return BadRequest(new { error = "Google ID token is required" });
        }

        try
        {
            var response = await _authService.LoginAsync(request.GoogleIdToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid Google token" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return StatusCode(500, new { error = "Authentication failed" });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GoogleIdToken))
            return BadRequest(new { error = "Google ID token is required" });

        try
        {
            var response = await _authService.RefreshAsync(request.GoogleIdToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid token" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed");
            return StatusCode(500, new { error = "Token refresh failed" });
        }
    }
}
