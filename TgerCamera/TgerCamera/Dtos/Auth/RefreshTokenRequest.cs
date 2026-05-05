using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Auth;

/// <summary>
/// Request DTO for refreshing access token using refresh token.
/// </summary>
public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = null!;
}
