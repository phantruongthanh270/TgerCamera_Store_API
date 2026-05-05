using TgerCamera.Dtos;

namespace TgerCamera.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAllAsync(
        int? brandId,
        int? categoryId,
        int? conditionId,
        decimal? minPrice,
        decimal? maxPrice,
        string? q,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDir);

    Task<ProductDto?> GetByIdAsync(int id);

    Task<ProductDto> CreateAsync(CreateProductRequestDto dto);

    Task<bool> UpdateAsync(int id, CreateProductRequestDto dto);

    Task<bool> DeleteAsync(int id);

    Task<ProductSpecificationDto?> AddSpecificationAsync(int productId, ProductSpecificationCreateDto dto);

    Task<bool> UpdateSpecificationAsync(int productId, int specificationId, ProductSpecificationCreateDto dto);

    Task<bool> DeleteSpecificationAsync(int productId, int specificationId);
}
