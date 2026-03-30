using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos;
using TgerCamera.Models;

namespace TgerCamera.Controllers;

/// <summary>
/// Handles brand-related operations including listing product brands.
/// Manages camera equipment manufacturers and brand information.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BrandController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the BrandController.
    /// </summary>
    /// <param name="context">The database context for accessing brand data.</param>
    /// <param name="mapper">AutoMapper instance for DTO mapping.</param>
    public BrandController(TgerCameraContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <summary>
    /// Retrieves a list of all product brands (excluding deleted ones).
    /// </summary>
    /// <returns>Returns a list of BrandDto for all active brands.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BrandDto>>> GetAll()
    {
        var items = await _context.Brands
            .Where(b => b.IsDeleted == null || b.IsDeleted == false)
            .ToListAsync();
        return Ok(_mapper.Map<IEnumerable<BrandDto>>(items));
    }
}
