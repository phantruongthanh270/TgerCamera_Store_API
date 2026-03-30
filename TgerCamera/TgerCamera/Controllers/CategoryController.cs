using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos;
using TgerCamera.Models;

namespace TgerCamera.Controllers;

/// <summary>
/// Handles category-related operations including listing product categories.
/// Supports product categorization with hierarchical parent-child relationships.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CategoryController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the CategoryController.
    /// </summary>
    /// <param name="context">The database context for accessing category data.</param>
    /// <param name="mapper">AutoMapper instance for DTO mapping.</param>
    public CategoryController(TgerCameraContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <summary>
    /// Retrieves a list of all product categories (excluding deleted ones).
    /// </summary>
    /// <returns>Returns a list of CategoryDto for all active categories.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll()
    {
        var items = await _context.Categories
            .Where(c => c.IsDeleted == null || c.IsDeleted == false)
            .ToListAsync();
        return Ok(_mapper.Map<IEnumerable<CategoryDto>>(items));
    }
}
