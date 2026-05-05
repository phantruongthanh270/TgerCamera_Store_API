using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos;
using TgerCamera.Models;

namespace TgerCamera.Services;

public class ProductService : IProductService
{
    private static readonly Dictionary<int, int[]> CategoryMappings = new()
    {
        { 1, new[] { 1, 4, 5, 10, 11 } },
        { 2, new[] { 2, 6, 7 } },
        { 3, new[] { 3, 8, 9 } },
        { 4, new[] { 4, 10, 11 } }
    };

    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;

    public ProductService(TgerCameraContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PagedResult<ProductDto>> GetAllAsync(
        int? brandId,
        int? categoryId,
        int? conditionId,
        decimal? minPrice,
        decimal? maxPrice,
        string? q,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDir)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = BuildProductQuery();

        if (brandId.HasValue)
        {
            query = query.Where(p => p.BrandId == brandId.Value);
        }

        if (categoryId.HasValue)
        {
            if (CategoryMappings.TryGetValue(categoryId.Value, out var categoryIds))
            {
                query = query.Where(p => categoryIds.Contains(p.CategoryId));
            }
            else
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }
        }

        if (conditionId.HasValue)
        {
            query = query.Where(p => p.ConditionId == conditionId.Value);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => p.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= maxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q}%";
            query = query.Where(p => EF.Functions.Like(p.Name, pattern));
        }

        var sortDirNormalized = (sortDir ?? "desc").ToLower();
        var ascending = sortDirNormalized == "asc";
        query = (sortBy ?? "createdAt").ToLower() switch
        {
            "price" => ascending ? query.OrderBy(p => p.Price) : query.OrderByDescending(p => p.Price),
            "name" => ascending ? query.OrderBy(p => p.Name) : query.OrderByDescending(p => p.Name),
            _ => ascending ? query.OrderBy(p => p.CreatedAt) : query.OrderByDescending(p => p.CreatedAt),
        };

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var dtos = _mapper.Map<IEnumerable<ProductDto>>(items);

        return new PagedResult<ProductDto>
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        var product = await BuildProductQuery().FirstOrDefaultAsync(p => p.Id == id);
        return product == null ? null : _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequestDto dto)
    {
        var entity = _mapper.Map<Product>(dto);

        if (!string.IsNullOrEmpty(dto.MainImageUrl))
        {
            entity.ProductImages = new List<ProductImage>
            {
                new ProductImage
                {
                    ImageUrl = dto.MainImageUrl,
                    IsMain = true
                }
            };
        }

        _context.Products.Add(entity);
        await _context.SaveChangesAsync();

        var created = await BuildProductQuery().FirstOrDefaultAsync(p => p.Id == entity.Id);
        return _mapper.Map<ProductDto>(created!);
    }

    public async Task<bool> UpdateAsync(int id, CreateProductRequestDto dto)
    {
        var entity = await _context.Products
            .Include(p => p.ProductImages)
            .Include(p => p.ProductSpecifications)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (entity == null)
        {
            return false;
        }

        entity.Name = dto.Name ?? "Unnamed Product";
        entity.Description = dto.Description;
        entity.Price = dto.Price;
        entity.StockQuantity = dto.StockQuantity;
        entity.BrandId = dto.BrandId;
        entity.CategoryId = dto.CategoryId;
        entity.ConditionId = dto.ConditionId;

        _context.ProductSpecifications.RemoveRange(entity.ProductSpecifications);
        entity.ProductSpecifications = dto.Specifications.Select(s => new ProductSpecification
        {
            Key = s.Key!,
            Value = s.Value!
        }).ToList();

        if (!string.IsNullOrEmpty(dto.MainImageUrl))
        {
            foreach (var img in entity.ProductImages)
            {
                img.IsMain = false;
                _context.ProductImages.Update(img);
            }

            var existing = entity.ProductImages.FirstOrDefault(pi => pi.ImageUrl == dto.MainImageUrl);
            if (existing != null)
            {
                existing.IsMain = true;
                _context.ProductImages.Update(existing);
            }
            else
            {
                var img = new ProductImage
                {
                    ImageUrl = dto.MainImageUrl,
                    IsMain = true,
                    ProductId = entity.Id
                };
                _context.ProductImages.Add(img);
                entity.ProductImages.Add(img);
            }
        }

        _context.Products.Update(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.Products.FindAsync(id);
        if (entity == null)
        {
            return false;
        }

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Products.Update(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ProductSpecificationDto?> AddSpecificationAsync(int productId, ProductSpecificationCreateDto dto)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
        {
            return null;
        }

        var spec = new ProductSpecification
        {
            ProductId = productId,
            Key = dto.Key!,
            Value = dto.Value!
        };

        _context.ProductSpecifications.Add(spec);
        await _context.SaveChangesAsync();

        return _mapper.Map<ProductSpecificationDto>(spec);
    }

    public async Task<bool> UpdateSpecificationAsync(int productId, int specificationId, ProductSpecificationCreateDto dto)
    {
        var spec = await _context.ProductSpecifications.FindAsync(specificationId);
        if (spec == null || spec.ProductId != productId)
        {
            return false;
        }

        spec.Key = dto.Key!;
        spec.Value = dto.Value!;
        _context.ProductSpecifications.Update(spec);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteSpecificationAsync(int productId, int specificationId)
    {
        var spec = await _context.ProductSpecifications.FindAsync(specificationId);
        if (spec == null || spec.ProductId != productId)
        {
            return false;
        }

        _context.ProductSpecifications.Remove(spec);
        await _context.SaveChangesAsync();
        return true;
    }

    private IQueryable<Product> BuildProductQuery()
    {
        return _context.Products
            .AsNoTracking()
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Condition)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductSpecifications)
            .Where(p => p.IsDeleted == null || p.IsDeleted == false);
    }
}
