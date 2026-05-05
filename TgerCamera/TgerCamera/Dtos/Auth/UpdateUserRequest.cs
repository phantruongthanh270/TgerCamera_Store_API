using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Auth;

/// <summary>
/// Request DTO for updating user information.
/// Admin can update user profile and address.
/// Email cannot be changed (immutable).
/// </summary>
public class UpdateUserRequest
{
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters")]
    public string? FullName { get; set; }

    [Phone(ErrorMessage = "Phone must be a valid phone number")]
    [StringLength(20, ErrorMessage = "Phone cannot exceed 20 characters")]
    public string? Phone { get; set; }

    [StringLength(400, ErrorMessage = "Address cannot exceed 400 characters")]
    public string? Address { get; set; }

    [RegularExpression("^(Customer|Admin)$", ErrorMessage = "Role is invalid")]
    public string? Role { get; set; }

    public string? Status { get; set; }
}
