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
/// Xử lý các thao tác rental product bao gồm liệt kê, lấy chi tiết, tạo, cập nhật và xoá rental products.
/// Cung cấp khả năng quản lý cho thuê product cùng theo dõi pricing và availability.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RentalProductController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;

    /// <summary>
    /// Khởi tạo một instance mới của RentalProductController.
    /// </summary>
    /// <param name="context">Database context dùng để truy cập dữ liệu rental product.</param>
    /// <param name="mapper">Instance AutoMapper dùng cho việc mapping DTO.</param>
    public RentalProductController(TgerCameraContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <summary>
    /// Lấy danh sách tất cả rental products đang khả dụng (không bao gồm item đã xoá).
    /// </summary>
    /// <returns>Trả về danh sách RentalProductDto cho toàn bộ rental products còn hoạt động.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RentalProductDto>>> GetAll()
    {
        var items = await _context.RentalProducts
            .Where(rp => rp.IsDeleted == null || rp.IsDeleted == false)
            .ToListAsync();
        return Ok(_mapper.Map<IEnumerable<RentalProductDto>>(items));
    }

    /// <summary>
    /// Lấy một rental product cụ thể theo ID.
    /// </summary>
    /// <param name="id">ID của rental product cần lấy.</param>
    /// <returns>Trả về RentalProductDto, hoặc NotFound nếu product không tồn tại hoặc đã bị xoá.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<RentalProductDto>> Get(int id)
    {
        var item = await _context.RentalProducts
            .FirstOrDefaultAsync(rp => rp.Id == id && (rp.IsDeleted == null || rp.IsDeleted == false));
        if (item == null) return NotFound();
        return Ok(_mapper.Map<RentalProductDto>(item));
    }

    /// <summary>
    /// Tạo mới một rental product. Chỉ dành cho Admin.
    /// </summary>
    /// <param name="dto">Thông tin rental product bao gồm pricing và availability.</param>
    /// <returns>Trả về RentalProductDto đã được tạo cùng ID, hoặc BadRequest nếu validation thất bại.</returns>
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
    /// Cập nhật một rental product hiện có. Chỉ dành cho Admin.
    /// </summary>
    /// <param name="id">ID của rental product cần cập nhật.</param>
    /// <param name="dto">Thông tin rental product đã được cập nhật.</param>
    /// <returns>Trả về NoContent nếu thành công, hoặc NotFound nếu product không tồn tại.</returns>
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
    /// Xoá một rental product bằng soft delete (đánh dấu đã xoá thay vì xoá khỏi database). Chỉ dành cho Admin.
    /// </summary>
    /// <param name="id">ID của rental product cần xoá.</param>
    /// <returns>Trả về NoContent nếu thành công, hoặc NotFound nếu product không tồn tại.</returns>
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
