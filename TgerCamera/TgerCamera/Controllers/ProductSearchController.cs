using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos;
using TgerCamera.Models;

namespace TgerCamera.Controllers;

[ApiController]
[Route("api/products")]
public class ProductSearchController : ControllerBase
{
    private readonly TgerCameraContext _context;

    public ProductSearchController(TgerCameraContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Tìm kiếm product để gợi ý autocomplete.
    /// </summary>
    /// <param name="q">Từ khoá tìm kiếm</param>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ProductSearchDto>>> Search([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(new List<ProductSearchDto>());
        }

        var keyword = q.Trim();
        var containsPattern = $"%{keyword}%";
        var startsWithPattern = $"{keyword}%";

        var items = await _context.Products
            .AsNoTracking()
            .Where(p => (p.IsDeleted == null || p.IsDeleted == false)
                        && EF.Functions.Like(p.Name, containsPattern))
            .OrderBy(p => EF.Functions.Like(p.Name, startsWithPattern) ? 0 : 1)
            .ThenBy(p => p.Name)
            .Select(p => new ProductSearchDto
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                MainImageUrl = p.ProductImages
                    .OrderByDescending(pi => pi.IsMain)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault()
            })
            .Take(10)
            .ToListAsync();

        return Ok(items);
    }
}
