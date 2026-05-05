using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Auth;

/// <summary>
/// Request DTO for email-based login.
/// </summary>
public class LoginRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = null!;
}
