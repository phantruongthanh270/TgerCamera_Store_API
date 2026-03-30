using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos;

public class CartItemUpdateDto
{
    [Required(ErrorMessage = "Quantity is required")]
    [Range(0, 1000, ErrorMessage = "Quantity must be between 0 and 1000")]
    public int Quantity { get; set; }
}
