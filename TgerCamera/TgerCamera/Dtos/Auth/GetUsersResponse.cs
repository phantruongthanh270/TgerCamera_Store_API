using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Auth;

/// <summary>
/// Response DTO for getting all users with pagination.
/// </summary>
public class GetUsersResponse
{
    [Required]
    public List<UserDto> Items { get; set; } = new List<UserDto>();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages { get; set; }
}
