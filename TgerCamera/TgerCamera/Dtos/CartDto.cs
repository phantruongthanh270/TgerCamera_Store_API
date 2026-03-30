using System.Collections.Generic;

namespace TgerCamera.Dtos;

public class CartDto
{
    public int Id { get; set; }
    public string? SessionId { get; set; }
    public int? UserId { get; set; }
    public List<CartItemDto> Items { get; set; } = new List<CartItemDto>();
}
