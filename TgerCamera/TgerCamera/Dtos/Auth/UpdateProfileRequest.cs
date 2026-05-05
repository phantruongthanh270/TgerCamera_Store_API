using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Auth;

/// <summary>
/// DTO for updating user profile information
/// </summary>
public class UpdateProfileRequest
{
    /// <summary>
    /// User's full name
    /// </summary>
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters")]
    public string? FullName { get; set; }

    /// <summary>
    /// User's phone number
    /// </summary>
    [Phone(ErrorMessage = "Phone must be a valid phone number")]
    [StringLength(20, ErrorMessage = "Phone cannot exceed 20 characters")]
    public string? Phone { get; set; }

    /// <summary>
    /// User's address
    /// </summary>
    [StringLength(255, ErrorMessage = "Address cannot exceed 255 characters")]
    public string? Address { get; set; }

    /// <summary>
    /// User's district
    /// </summary>
    [StringLength(100, ErrorMessage = "District cannot exceed 100 characters")]
    public string? District { get; set; }

    /// <summary>
    /// User's city
    /// </summary>
    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters")]
    public string? City { get; set; }
}
