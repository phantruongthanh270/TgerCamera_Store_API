using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos;
using TgerCamera.Models;

namespace TgerCamera.Controllers;

/// <summary>
/// Xử lý các thao tác liên quan đến brand bao gồm liệt kê product brands.
/// Quản lý thông tin brand và nhà sản xuất thiết bị camera.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BrandController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;

    /// <summary>
    /// Khởi tạo một instance mới của BrandController.
    /// </summary>
    /// <param name="context">Database context dùng để truy cập dữ liệu brand.</param>
    /// <param name="mapper">Instance AutoMapper dùng cho việc mapping DTO.</param>
    public BrandController(TgerCameraContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <summary>
    /// Lấy danh sách tất cả product brands (không bao gồm item đã xoá).
    /// </summary>
    /// <returns>Trả về danh sách BrandDto cho toàn bộ brands còn hoạt động.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BrandDto>>> GetAll()
    {
        var items = await _context.Brands
            .Where(b => b.IsDeleted == null || b.IsDeleted == false)
            .ToListAsync();
        return Ok(_mapper.Map<IEnumerable<BrandDto>>(items));
    }
}
