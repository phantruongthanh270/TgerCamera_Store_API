using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos;

public class CreateProductRequestDto
{
    [Required(ErrorMessage = "Product name is required")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Product name must be between 3 and 200 characters")]
    public string? Name { get; set; }

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Price is required")]
    [Range(0.01, 999999999, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Stock quantity is required")]
    [Range(0, 999999, ErrorMessage = "Stock quantity must be between 0 and 999999")]
    public int StockQuantity { get; set; }

    [Required(ErrorMessage = "Brand ID is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Brand ID must be valid")]
    public int BrandId { get; set; }

    [Required(ErrorMessage = "Category ID is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Category ID must be valid")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Condition ID is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Condition ID must be valid")]
    public int ConditionId { get; set; }

    [Url(ErrorMessage = "Main image URL must be a valid URL")]
    public string? MainImageUrl { get; set; }

    public List<ProductSpecificationCreateDto> Specifications { get; set; } = new List<ProductSpecificationCreateDto>();
}
