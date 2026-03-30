namespace TgerCamera.Dtos;

public class ProductDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }

    // Simple nested objects containing Id and Name
    public BrandDto? Brand { get; set; }
    public CategoryDto? Category { get; set; }
    public ProductConditionDto? Condition { get; set; }

    // Main image URL (if any)
    public string? MainImageUrl { get; set; }

    // Product specifications
    public List<ProductSpecificationDto> Specifications { get; set; } = new List<ProductSpecificationDto>();
}
