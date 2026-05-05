using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Auth;

/// <summary>
/// Response DTO for authentication operations.
/// Contains both access token and refresh token for improved security.
/// </summary>
public class AuthResponse
{
    [Required]
    public string AccessToken { get; set; } = null!;

    [Required]
    public string RefreshToken { get; set; } = null!;

    [Required]
    public string TokenType { get; set; } = "Bearer";

    public int ExpiresIn { get; set; }

    public UserResponse? User { get; set; }
}

/// <summary>
/// User information in auth response.
/// </summary>
public class UserResponse
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = null!;
}
