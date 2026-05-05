using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos;
using TgerCamera.Models;

namespace TgerCamera.Controllers;

/// <summary>
/// Xử lý các thao tác liên quan đến category bao gồm liệt kê product categories.
/// Hỗ trợ phân loại product với parent-child hierarchy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CategoryController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;

    /// <summary>
    /// Khởi tạo một instance mới của CategoryController.
    /// </summary>
    /// <param name="context">Database context dùng để truy cập dữ liệu category.</param>
    /// <param name="mapper">Instance AutoMapper dùng cho việc mapping DTO.</param>
    public CategoryController(TgerCameraContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <summary>
    /// Lấy danh sách tất cả product categories (không bao gồm item đã xoá).
    /// </summary>
    /// <returns>Trả về danh sách CategoryDto cho toàn bộ categories còn hoạt động.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll()
    {
        var items = await _context.Categories
            .Where(c => c.IsDeleted == null || c.IsDeleted == false)
            .ToListAsync();
        return Ok(_mapper.Map<IEnumerable<CategoryDto>>(items));
    }
}
