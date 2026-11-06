namespace StockSyncApi.DTOs;

public class LoginRequest
{
    public string GoogleIdToken { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string JwtToken { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class RefreshRequest
{
    public string GoogleIdToken { get; set; } = string.Empty;
}

public class RefreshResponse
{
    public string JwtToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
