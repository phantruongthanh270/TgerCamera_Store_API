using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Order;

public class CheckoutDto
{
    [Required(ErrorMessage = "Shipping address is required")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Address must be between 5 and 500 characters")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Phone must be a valid phone number")]
    public string? Phone { get; set; }
}
