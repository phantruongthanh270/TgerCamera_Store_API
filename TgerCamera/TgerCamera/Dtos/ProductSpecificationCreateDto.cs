using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos;

public class ProductSpecificationCreateDto
{
    [Required(ErrorMessage = "Specification key is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Key must be between 1 and 100 characters")]
    public string? Key { get; set; }

    [Required(ErrorMessage = "Specification value is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Value must be between 1 and 500 characters")]
    public string? Value { get; set; }
}
