namespace TgerCamera.Dtos;

public class UserDto
{
    public int Id { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Role { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
