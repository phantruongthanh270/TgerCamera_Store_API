using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Auth;

/// <summary>
/// Request DTO for user registration with email and password.
/// </summary>
public class RegisterRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Full name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters")]
    public string FullName { get; set; } = null!;

    [Phone(ErrorMessage = "Phone must be a valid phone number")]
    public string? Phone { get; set; }
}
