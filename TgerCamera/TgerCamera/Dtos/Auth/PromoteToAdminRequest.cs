using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Auth;

/// <summary>
/// Request DTO for promoting user to Admin role.
/// TEMPORARY - Remove in production!
/// </summary>
public class PromoteToAdminRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
}