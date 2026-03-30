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
/// Handles rental product operations including listing, retrieving, creating, updating, and deleting rental products.
/// Provides management of product rental capabilities with pricing and availability tracking.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RentalProductController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the RentalProductController.
    /// </summary>
    /// <param name="context">The database context for accessing rental product data.</param>
    /// <param name="mapper">AutoMapper instance for DTO mapping.</param>
    public RentalProductController(TgerCameraContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <summary>
    /// Retrieves a list of all available rental products (excluding deleted ones).
    /// </summary>
    /// <returns>Returns a list of RentalProductDto for all active rental products.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RentalProductDto>>> GetAll()
    {
        var items = await _context.RentalProducts
            .Where(rp => rp.IsDeleted == null || rp.IsDeleted == false)
            .ToListAsync();
        return Ok(_mapper.Map<IEnumerable<RentalProductDto>>(items));
    }

    /// <summary>
    /// Retrieves a specific rental product by ID.
    /// </summary>
    /// <param name="id">The rental product ID to retrieve.</param>
    /// <returns>Returns the RentalProductDto, or NotFound if product doesn't exist or is deleted.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<RentalProductDto>> Get(int id)
    {
        var item = await _context.RentalProducts
            .FirstOrDefaultAsync(rp => rp.Id == id && (rp.IsDeleted == null || rp.IsDeleted == false));
        if (item == null) return NotFound();
        return Ok(_mapper.Map<RentalProductDto>(item));
    }

    /// <summary>
    /// Creates a new rental product. Admin only.
    /// </summary>
    /// <param name="dto">The rental product details including pricing and availability.</param>
    /// <returns>Returns the created RentalProductDto with ID, or BadRequest if validation fails.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RentalProductDto>> Create(RentalProductDto dto)
    {
        var entity = _mapper.Map<RentalProduct>(dto);
        _context.RentalProducts.Add(entity);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, _mapper.Map<RentalProductDto>(entity));
    }

    /// <summary>
    /// Updates an existing rental product. Admin only.
    /// </summary>
    /// <param name="id">The rental product ID to update.</param>
    /// <param name="dto">The updated rental product details.</param>
    /// <returns>Returns NoContent on success, or NotFound if product doesn't exist.</returns>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, RentalProductDto dto)
    {
        var entity = await _context.RentalProducts.FindAsync(id);
        if (entity == null) return NotFound();
        _mapper.Map(dto, entity);
        entity.Id = id;
        _context.RentalProducts.Update(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Deletes a rental product using soft delete (marks as deleted instead of removing from database). Admin only.
    /// </summary>
    /// <param name="id">The rental product ID to delete.</param>
    /// <returns>Returns NoContent on success, or NotFound if product doesn't exist.</returns>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.RentalProducts.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        _context.RentalProducts.Update(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
