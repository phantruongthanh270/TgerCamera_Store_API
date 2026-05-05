namespace TgerCamera.Dtos;

public class ProductDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }

    // Các nested object đơn giản chỉ chứa Id và Name
    public BrandDto? Brand { get; set; }
    public CategoryDto? Category { get; set; }
    public ProductConditionDto? Condition { get; set; }

    // URL của ảnh chính (nếu có)
    public string? MainImageUrl { get; set; }

    // Danh sách product specifications
    public List<ProductSpecificationDto> Specifications { get; set; } = new List<ProductSpecificationDto>();
}
