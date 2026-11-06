using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StockSyncApi.Data;
using StockSyncApi.Models;
using StockSyncApi.DTOs;

namespace StockSyncApi.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(string googleIdToken);
    Task<RefreshResponse> RefreshAsync(string googleIdToken);
}

public class AuthService : IAuthService
{
    private readonly StockSyncDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(StockSyncDbContext db, IConfiguration config, ILogger<AuthService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(string googleIdToken)
    {
        if (string.IsNullOrWhiteSpace(googleIdToken))
            throw new ArgumentException("Google ID token is required");

        _logger.LogInformation("Login attempt received");

        var payload = await ValidateGoogleTokenAsync(googleIdToken);
        var googleId = payload.Subject;
        var displayName = payload.Name ?? payload.Email ?? "StockSync User";
        var email = payload.Email ?? throw new UnauthorizedAccessException("Google account email is missing");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);

        if (user == null)
        {
            _logger.LogInformation("Creating new user for GoogleId {GoogleId}", googleId);
            user = new User
            {
                Id = Guid.NewGuid(),
                GoogleId = googleId,
                DisplayName = displayName,
                Email = email,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);

            var settings = new Settings
            {
                UserId = user.Id,
                Language = "en",
                Currency = "ZAR",
                LowStockThreshold = 5,
                NotifyLowStock = true,
                NotifyDailySummary = true,
                NotifySyncComplete = false,
                BiometricLock = true
            };
            _db.Settings.Add(settings);

            await _db.SaveChangesAsync();
            _logger.LogInformation("User {UserId} created with default settings", user.Id);
        }
        else
        {
            _logger.LogInformation("Existing user {UserId} logged in", user.Id);
        }

        var jwt = GenerateJwt(user.Id);
        return new LoginResponse
        {
            JwtToken = jwt,
            UserId = user.Id.ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
    }

    public async Task<RefreshResponse> RefreshAsync(string googleIdToken)
    {
        _logger.LogInformation("Token refresh requested");
        var payload = await ValidateGoogleTokenAsync(googleIdToken);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject)
            ?? throw new UnauthorizedAccessException("User not found for refresh");

        var jwt = GenerateJwt(user.Id);
        return new RefreshResponse
        {
            JwtToken = jwt,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
    }

    private string GenerateJwt(Guid userId)
    {
        var keyString = _config["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY");
        if (string.IsNullOrEmpty(keyString) || keyString.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key not configured. Set Jwt:Key in config or JWT_KEY env var (min 32 chars).");
        }
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "StockSync",
            audience: _config["Jwt:Audience"] ?? "StockSyncApp",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<GoogleJsonWebSignature.Payload> ValidateGoogleTokenAsync(string token)
    {
        var clientId = _config["Google:ClientId"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Google client ID is not configured");

        try
        {
            return await GoogleJsonWebSignature.ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedAccessException("Invalid Google token");
        }
    }
}
