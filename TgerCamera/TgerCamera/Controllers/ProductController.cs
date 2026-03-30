using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos;
using TgerCamera.Models;

namespace TgerCamera.Controllers;

/// <summary>
/// Handles product-related operations including listing products with filters, retrieving product details,
/// creating/updating/deleting products (admin only), and managing product specifications.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the ProductController.
    /// </summary>
    /// <param name="context">The database context for accessing product data.</param>
    /// <param name="mapper">AutoMapper instance for DTO mapping.</param>
    public ProductController(TgerCameraContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <summary>
    /// Retrieves a paginated list of products with support for filtering, searching, and sorting.
    /// Supports filtering by brand, category (with parent-child hierarchy), condition, and price range.
    /// </summary>
    /// <param name="brandId">Optional: Filter by brand ID.</param>
    /// <param name="categoryId">Optional: Filter by category ID (includes child categories for parent categories).</param>
    /// <param name="conditionId">Optional: Filter by product condition ID.</param>
    /// <param name="minPrice">Optional: Minimum price filter.</param>
    /// <param name="maxPrice">Optional: Maximum price filter.</param>
    /// <param name="q">Optional: Search query to filter by product name.</param>
    /// <param name="page">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 200).</param>
    /// <param name="sortBy">Field to sort by: "price", "name", or "createdAt" (default: "createdAt").</param>
    /// <param name="sortDir">Sort direction: "asc" for ascending or "desc" for descending (default: "desc").</param>
    /// <returns>Returns a PagedResult containing the filtered product list and pagination metadata.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
        [FromQuery] int? brandId,
        [FromQuery] int? categoryId,
        [FromQuery] int? conditionId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = "createdAt",
        [FromQuery] string? sortDir = "desc")
    {
        if (minPrice.HasValue && maxPrice.HasValue && minPrice.Value > maxPrice.Value)
        {
            return BadRequest("minPrice must be less than or equal to maxPrice.");
        }
        var query = _context.Products.AsQueryable();

        // include navigation properties for mapping
        query = query.Include(p => p.Brand)
                     .Include(p => p.Category)
                     .Include(p => p.Condition)
                     .Include(p => p.ProductImages)
                     .Include(p => p.ProductSpecifications);

        // Filter out soft-deleted products
        query = query.Where(p => p.IsDeleted == null || p.IsDeleted == false);

        if (brandId.HasValue)
        {
            query = query.Where(p => p.BrandId == brandId.Value);
        }

        if (categoryId.HasValue)
        {
            // Define parent-child relationships
            var categoryMappings = new Dictionary<int, int[]>
            {
                { 1, new[] { 1, 4, 5, 10, 11 } }, // Máy ảnh includes itself and children (Mirrorless, DSLR, Crop, Full-Frame)
                { 2, new[] { 2, 6, 7 } }, // Ống kính includes itself and children (Sony, Canon)
                { 3, new[] { 3, 8, 9 } },  // Phụ kiện includes itself and children (Thẻ nhớ, Pin & Sạc)
                { 4, new[] { 4, 10, 11 } }  // Mirrorless includes itself and children (Crop, Full-Frame)
            };

            if (categoryMappings.ContainsKey(categoryId.Value))
            {
                // If it's a parent category, include all its children
                var categoryIds = categoryMappings[categoryId.Value];
                query = query.Where(p => categoryIds.Contains(p.CategoryId));
            }
            else
            {
                // If it's a child category, filter by exact match
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

        // sorting
        var sortDirNormalized = (sortDir ?? "desc").ToLower();
        var ascending = sortDirNormalized == "asc";
        query = (sortBy ?? "createdAt").ToLower() switch
        {
            "price" => ascending ? query.OrderBy(p => p.Price) : query.OrderByDescending(p => p.Price),
            "name" => ascending ? query.OrderBy(p => p.Name) : query.OrderByDescending(p => p.Name),
            _ => ascending ? query.OrderBy(p => p.CreatedAt) : query.OrderByDescending(p => p.CreatedAt),
        };

        var totalItems = await query.CountAsync();
        var totalPages = (int)System.Math.Ceiling(totalItems / (double)pageSize);
        page = System.Math.Max(1, page);
        pageSize = System.Math.Clamp(pageSize, 1, 200);

        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var dtos = _mapper.Map<IEnumerable<ProductDto>>(items);

        var result = new PagedResult<ProductDto>
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific product by ID with all related information including images and specifications.
    /// </summary>
    /// <param name="id">The product ID to retrieve.</param>
    /// <returns>Returns the ProductDto with full details, or NotFound if product doesn't exist.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> Get(int id)
    {
        var product = await _context.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Condition)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductSpecifications)
            .FirstOrDefaultAsync(p => p.Id == id && (p.IsDeleted == null || p.IsDeleted == false));
        if (product == null) return NotFound();
        return Ok(_mapper.Map<ProductDto>(product));
    }

    /// <summary>
    /// Creates a new product. Admin only.
    /// </summary>
    /// <param name="dto">The product creation request containing product details.</param>
    /// <returns>Returns the created ProductDto with ID, or BadRequest if validation fails.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequestDto dto)
    {
        var entity = _mapper.Map<Product>(dto);

        // add main image if provided
        if (!string.IsNullOrEmpty(dto.MainImageUrl))
        {
            entity.ProductImages = new List<ProductImage> { new ProductImage { ImageUrl = dto.MainImageUrl, IsMain = true } };
        }

        _context.Products.Add(entity);
        await _context.SaveChangesAsync();

        // reload with navigation properties for mapping
        var created = await _context.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Condition)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductSpecifications)
            .FirstOrDefaultAsync(p => p.Id == entity.Id);

        var resultDto = _mapper.Map<ProductDto>(created!);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, resultDto);
    }

    /// <summary>
    /// Updates an existing product with new information.
    /// </summary>
    /// <param name="id">The product ID to update.</param>
    /// <param name="dto">The update request containing updated product details.</param>
    /// <returns>Returns NoContent on success, or NotFound if product doesn't exist.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CreateProductRequestDto dto)
    {
        var entity = await _context.Products
            .Include(p => p.ProductImages)
            .Include(p => p.ProductSpecifications)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (entity == null) return NotFound();

        // update scalar fields
        entity.Name = dto.Name ?? "Unnamed Product";
        entity.Description = dto.Description;
        entity.Price = dto.Price;
        entity.StockQuantity = dto.StockQuantity;
        entity.BrandId = dto.BrandId;
        entity.CategoryId = dto.CategoryId;
        entity.ConditionId = dto.ConditionId;

        // update specifications: replace existing with new list
        _context.ProductSpecifications.RemoveRange(entity.ProductSpecifications);
        entity.ProductSpecifications = dto.Specifications.Select(s => new ProductSpecification
        {
            Key = s.Key,
            Value = s.Value
        }).ToList();

        // update main image if provided
        if (!string.IsNullOrEmpty(dto.MainImageUrl))
        {
            // unset existing main
            foreach (var img in entity.ProductImages)
            {
                img.IsMain = false;
                _context.ProductImages.Update(img);
            }

            var existing = entity.ProductImages.FirstOrDefault(pi => pi.ImageUrl == dto.MainImageUrl);
            if (existing != null)
            {
                existing!.IsMain = true;
                _context.ProductImages.Update(existing!);
            }
            else
            {
                var img = new ProductImage { ImageUrl = dto.MainImageUrl, IsMain = true, ProductId = entity.Id };
                _context.ProductImages.Add(img);
                entity.ProductImages.Add(img);
            }
        }

        _context.Products.Update(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Deletes a product by ID using soft delete (marks as deleted instead of removing from database).
    /// </summary>
    /// <param name="id">The product ID to delete.</param>
    /// <returns>Returns NoContent on success, or NotFound if product doesn't exist.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.Products.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = System.DateTime.UtcNow;
        _context.Products.Update(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Adds a new specification to an existing product.
    /// </summary>
    /// <param name="productId">The product ID to add specification to.</param>
    /// <param name="dto">The specification details containing key and value.</param>
    /// <returns>Returns the created ProductSpecificationDto, NotFound if product doesn't exist, or BadRequest if validation fails.</returns>
    // Specifications management
    [HttpPost("{productId}/specifications")]
    public async Task<ActionResult<ProductSpecificationDto>> AddSpecification(int productId, ProductSpecificationCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Key) || string.IsNullOrWhiteSpace(dto.Value))
            return BadRequest("Specification key and value are required.");

        var product = await _context.Products.FindAsync(productId);
        if (product == null) return NotFound();

        var spec = new ProductSpecification { ProductId = productId, Key = dto.Key, Value = dto.Value };
        _context.ProductSpecifications.Add(spec);
        await _context.SaveChangesAsync();

        var specDto = _mapper.Map<ProductSpecificationDto>(spec);
        return CreatedAtAction(nameof(Get), new { id = productId }, specDto);
    }

    [HttpPut("{productId}/specifications/{id}")]
    public async Task<IActionResult> UpdateSpecification(int productId, int id, ProductSpecificationCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Key) || string.IsNullOrWhiteSpace(dto.Value))
            return BadRequest("Specification key and value are required.");

        var spec = await _context.ProductSpecifications.FindAsync(id);
        if (spec == null || spec.ProductId != productId) return NotFound();

        spec.Key = dto.Key;
        spec.Value = dto.Value;
        _context.ProductSpecifications.Update(spec);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{productId}/specifications/{id}")]
    public async Task<IActionResult> DeleteSpecification(int productId, int id)
    {
        var spec = await _context.ProductSpecifications.FindAsync(id);
        if (spec == null || spec.ProductId != productId) return NotFound();

        _context.ProductSpecifications.Remove(spec);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
