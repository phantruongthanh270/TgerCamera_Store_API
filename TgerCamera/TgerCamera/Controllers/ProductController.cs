using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TgerCamera.Dtos;
using TgerCamera.Services;

namespace TgerCamera.Controllers;

/// <summary>
/// Xử lý các thao tác liên quan đến product bao gồm liệt kê product với filter, lấy chi tiết product,
/// tạo/cập nhật/xoá product (chỉ Admin), và quản lý product specifications.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;

    /// <summary>
    /// Khởi tạo một instance mới của ProductController.
    /// </summary>
    /// <param name="productService">Service xử lý các thao tác dữ liệu product.</param>
    public ProductController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Lấy danh sách product theo pagination, hỗ trợ filtering, searching và sorting.
    /// Hỗ trợ filter theo brand, category (có parent-child hierarchy), condition và khoảng giá.
    /// </summary>
    /// <param name="brandId">Tuỳ chọn: filter theo ID của brand.</param>
    /// <param name="categoryId">Tuỳ chọn: filter theo ID của category (bao gồm category con nếu là category cha).</param>
    /// <param name="conditionId">Tuỳ chọn: filter theo ID của product condition.</param>
    /// <param name="minPrice">Tuỳ chọn: filter giá tối thiểu.</param>
    /// <param name="maxPrice">Tuỳ chọn: filter giá tối đa.</param>
    /// <param name="q">Tuỳ chọn: search query để filter theo tên product.</param>
    /// <param name="page">Số trang cho pagination (mặc định: 1).</param>
    /// <param name="pageSize">Số item trên mỗi trang (mặc định: 20, tối đa: 200).</param>
    /// <param name="sortBy">Field dùng để sort: "price", "name", hoặc "createdAt" (mặc định: "createdAt").</param>
    /// <param name="sortDir">Chiều sort: "asc" tăng dần hoặc "desc" giảm dần (mặc định: "desc").</param>
    /// <returns>Trả về một PagedResult chứa danh sách product đã filter và metadata của pagination.</returns>
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

        var result = await _productService.GetAllAsync(
            brandId,
            categoryId,
            conditionId,
            minPrice,
            maxPrice,
            q,
            page,
            pageSize,
            sortBy,
            sortDir);

        return Ok(result);
    }

    /// <summary>
    /// Lấy một product cụ thể theo ID cùng toàn bộ thông tin liên quan bao gồm images và specifications.
    /// </summary>
    /// <param name="id">ID của product cần lấy.</param>
    /// <returns>Trả về ProductDto với đầy đủ chi tiết, hoặc NotFound nếu product không tồn tại.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> Get(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null) return NotFound();
        return Ok(product);
    }

    /// <summary>
    /// Tạo mới một product. Chỉ dành cho Admin.
    /// </summary>
    /// <param name="dto">Request tạo product chứa thông tin chi tiết của product.</param>
    /// <returns>Trả về ProductDto đã được tạo cùng ID, hoặc BadRequest nếu validation thất bại.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequestDto dto)
    {
        var created = await _productService.CreateAsync(dto);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Cập nhật một product hiện có bằng thông tin mới.
    /// </summary>
    /// <param name="id">ID của product cần cập nhật.</param>
    /// <param name="dto">Request cập nhật chứa thông tin product mới.</param>
    /// <returns>Trả về NoContent nếu thành công, hoặc NotFound nếu product không tồn tại.</returns>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, CreateProductRequestDto dto)
    {
        var updated = await _productService.UpdateAsync(id, dto);
        if (!updated) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Xoá một product theo ID bằng soft delete (đánh dấu đã xoá thay vì xoá khỏi database).
    /// </summary>
    /// <param name="id">ID của product cần xoá.</param>
    /// <returns>Trả về NoContent nếu thành công, hoặc NotFound nếu product không tồn tại.</returns>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _productService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Thêm một specification mới vào product hiện có.
    /// </summary>
    /// <param name="productId">ID của product cần thêm specification.</param>
    /// <param name="dto">Thông tin specification bao gồm key và value.</param>
    /// <returns>Trả về ProductSpecificationDto đã được tạo, NotFound nếu product không tồn tại, hoặc BadRequest nếu validation thất bại.</returns>
    // Quản lý specifications
    [HttpPost("{productId}/specifications")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductSpecificationDto>> AddSpecification(int productId, ProductSpecificationCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Key) || string.IsNullOrWhiteSpace(dto.Value))
            return BadRequest("Specification key and value are required.");

        var spec = await _productService.AddSpecificationAsync(productId, dto);
        if (spec == null) return NotFound();

        return CreatedAtAction(nameof(Get), new { id = productId }, spec);
    }

    [HttpPut("{productId}/specifications/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSpecification(int productId, int id, ProductSpecificationCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Key) || string.IsNullOrWhiteSpace(dto.Value))
            return BadRequest("Specification key and value are required.");

        var updated = await _productService.UpdateSpecificationAsync(productId, id, dto);
        if (!updated) return NotFound();

        return NoContent();
    }

    [HttpDelete("{productId}/specifications/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSpecification(int productId, int id)
    {
        var deleted = await _productService.DeleteSpecificationAsync(productId, id);
        if (!deleted) return NotFound();

        return NoContent();
    }
}
