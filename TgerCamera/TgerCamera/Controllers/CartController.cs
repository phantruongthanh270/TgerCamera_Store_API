using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos;
using TgerCamera.Models;
using TgerCamera.Services;

namespace TgerCamera.Controllers;

/// <summary>
/// Xử lý các thao tác shopping cart bao gồm lấy cart, thêm item, cập nhật quantity và xoá item.
/// Hỗ trợ cả guest cart (qua cookie SessionId trong Distributed Cache) và cart của authenticated user (trong Database).
/// 
/// Kiến trúc:
/// - Guest Carts: được lưu trong Distributed Cache (Redis/MemoryCache) với TTL 24 giờ, key format: "cart:{sessionId}"
/// - User Carts: được lưu trong SQL Server database, khoá theo UserId
/// - Merge Logic: khi authenticated user truy cập cart, guest cart sẽ tự động merge vào user cart với cơ chế cộng dồn quantity
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;
    private readonly ICartService _cartService;
    private const string SESSION_COOKIE_NAME = "SessionId";
    private const int SESSION_COOKIE_EXPIRATION_DAYS = 15;

    /// <summary>
    /// Khởi tạo một instance mới của CartController.
    /// </summary>
    public CartController(TgerCameraContext context, IMapper mapper, ICartService cartService)
    {
        _context = context;
        _mapper = mapper;
        _cartService = cartService;
    }

    /// <summary>
    /// Trích xuất SessionId từ request cookies hoặc từ custom header X-Session-Id.
    /// Điều này cho phép guest cart requests vẫn hoạt động ngay cả khi trình duyệt không giữ cross-origin cookies.
    /// </summary>
    private string? ReadSessionIdFromCookie()
    {
        if (Request.Cookies.TryGetValue(SESSION_COOKIE_NAME, out var sessionId) && !string.IsNullOrEmpty(sessionId))
        {
            return sessionId;
        }

        if (Request.Headers.TryGetValue("X-Session-Id", out var sessionIdHeader) && !string.IsNullOrEmpty(sessionIdHeader))
        {
            return sessionIdHeader.ToString();
        }

        return null;
    }

    /// <summary>
    /// Gán cookie SessionId vào response với HttpOnly, Lax SameSite và thời hạn 30 ngày.
    /// </summary>
    private void SetSessionIdCookie(string sessionId)
    {
        Response.Cookies.Append(SESSION_COOKIE_NAME, sessionId, new Microsoft.AspNetCore.Http.CookieOptions
        {
            HttpOnly = true,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(SESSION_COOKIE_EXPIRATION_DAYS)
        });
    }

    /// <summary>
    /// Xoá cookie SessionId khi merge guest cart sang user cart.
    /// </summary>
    private void DeleteSessionIdCookie()
    {
        Response.Cookies.Delete(SESSION_COOKIE_NAME);
    }

    /// <summary>
    /// Trích xuất UserId từ JWT claims.
    /// Thử cả claim NameIdentifier chuẩn và claim Sub của JWT.
    /// </summary>
    private int? GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (int.TryParse(userIdClaim, out var userId))
            return userId;

        return null;
    }

    /// <summary>
    /// Lấy shopping cart cho session hiện tại hoặc authenticated user hiện tại.
    /// 
    /// Luồng xử lý:
    /// 1. Nếu user CHƯA authenticated: trả về guest cart từ cache (hoặc empty cart)
    /// 2. Nếu user ĐÃ authenticated:
    ///    a. Merge guest cart từ cache vào database cart của user (nếu guest cart tồn tại)
    ///    b. Xoá cookie SessionId và clear cache sau khi merge
    ///    c. Trả về database cart của user
    /// </summary>
    /// <returns>CartDto chứa toàn bộ cart items và metadata</returns>
    /// <response code="200">Trả về cart của user hoặc guest cùng các items</response>
    [HttpGet]
    public async Task<ActionResult<CartDto>> Get()
    {
        var sessionId = ReadSessionIdFromCookie();
        var userId = GetUserIdFromClaims();

        CartDto? cart = null;

        // Nếu user đã authenticated: thử merge guest cart và trả về user cart
        if (userId.HasValue)
        {
            // Merge guest cart (nếu có trong cache) vào database cart của user
            if (!string.IsNullOrEmpty(sessionId))
            {
                await _cartService.MergeGuestCartToUserAsync(userId.Value, sessionId);
                DeleteSessionIdCookie();
            }

            // Trả về database cart của user
            cart = await _cartService.GetUserCartAsync(userId.Value);
            if (cart == null)
            {
                cart = new CartDto { UserId = userId.Value, Items = new List<CartItemDto>() };
            }
        }
        else
        {
            // Guest user: trả về cart từ cache hoặc empty cart
            if (!string.IsNullOrEmpty(sessionId))
            {
                cart = await _cartService.GetGuestCartAsync(sessionId);
            }

            if (cart == null)
            {
                // Tạo guest session mới
                sessionId = Guid.NewGuid().ToString();
                cart = new CartDto { SessionId = sessionId, Items = new List<CartItemDto>() };
                SetSessionIdCookie(sessionId);
            }
            else if (string.IsNullOrEmpty(sessionId))
            {
                SetSessionIdCookie(cart.SessionId!);
            }
        }

        return Ok(cart);
    }

    /// <summary>
    /// Thêm một product vào shopping cart hoặc tăng quantity nếu item đã tồn tại.
    /// 
    /// Validation:
    /// - Quantity phải lớn hơn 0
    /// - Product phải tồn tại và chưa bị xoá
    /// - Stock phải đủ
    /// - Tổng quantity sau khi thêm không được vượt quá stock hiện có
    /// </summary>
    /// <param name="dto">Request add to cart chứa product ID và quantity mong muốn</param>
    /// <returns>CartDto đã cập nhật với item mới</returns>
    /// <response code="200">Thêm/cập nhật item thành công</response>
    /// <response code="400">Validation thất bại (product không hợp lệ, không đủ stock, quantity không hợp lệ)</response>
    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem([FromBody] CartItemCreateDto dto)
    {
        try
        {
            if (dto.Quantity <= 0)
                return BadRequest(new { message = "Quantity must be greater than 0." });

            var sessionId = ReadSessionIdFromCookie();
            var userId = GetUserIdFromClaims();

            // Thêm vào storage phù hợp (cache cho guest, database cho user)
            var updatedCart = await _cartService.AddItemToCacheOrDbAsync(sessionId, userId, dto.ProductId, dto.Quantity);

            // Với guest user: đảm bảo SessionId được gán vào cookie
            if (!userId.HasValue && !string.IsNullOrEmpty(updatedCart.SessionId))
            {
                SetSessionIdCookie(updatedCart.SessionId);
            }

            return Ok(updatedCart);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật quantity của một cart item hiện có.
    /// Với guest cart: cập nhật item trong cache (ID &lt; 0)
    /// Với user cart: cập nhật item trong database (ID &gt; 0)
    /// Validation để đảm bảo quantity không vượt quá stock của product.
    /// </summary>
    /// <param name="id">ID của cart item cần cập nhật (âm cho guest, dương cho user)</param>
    /// <param name="dto">Request cập nhật chứa quantity mới</param>
    /// <returns>NoContent nếu thành công</returns>
    /// <response code="204">Cập nhật quantity thành công</response>
    /// <response code="400">Quantity không hợp lệ hoặc vượt quá stock</response>
    /// <response code="404">Cart item không tồn tại hoặc không thuộc về user</response>
    [HttpPut("items/{id}")]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] CartItemUpdateDto dto)
    {
        if (dto.Quantity <= 0)
            return BadRequest(new { message = "Quantity must be greater than 0." });

        var sessionId = ReadSessionIdFromCookie();
        var userId = GetUserIdFromClaims();

        // Xử lý guest cart items (ID âm)
        if (id < 0)
        {
            if (string.IsNullOrEmpty(sessionId))
                return Unauthorized(new { message = "SessionId required for guest cart." });

            var guestCart = await _cartService.GetGuestCartAsync(sessionId);
            if (guestCart == null)
                return NotFound();

            var item = guestCart.Items.FirstOrDefault(i => i.Id == id);
            if (item == null)
                return NotFound();

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == item.ProductId && (p.IsDeleted == null || p.IsDeleted == false));
            if (product == null)
                return BadRequest(new { message = "Sáº£n pháº©m khÃ´ng cÃ²n tá»“n táº¡i." });

            if (product.StockQuantity < dto.Quantity)
                return BadRequest(new { message = $"KhÃ´ng Ä‘á»§ tá»“n kho. Sá»‘ lÆ°á»£ng hiá»‡n cÃ³: {product.StockQuantity}." });

            // Cập nhật quantity và lưu lại vào cache
            item.Quantity = dto.Quantity;
            await _cartService.SaveGuestCartAsync(sessionId, guestCart);
            return NoContent();
        }

        // Xử lý user cart items (ID dương)
        if (!userId.HasValue)
            return Unauthorized(new { message = "Authentication is required to update a saved cart item." });

        var dbItem = await _context.CartItems
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.Id == id);

        if (dbItem == null)
            return NotFound();

        // Xác minh item thuộc về user hiện tại
        {
            var cartBelongsToUser = await _context.Carts
                .AnyAsync(c => c.Id == dbItem.CartId && c.UserId == userId);

            if (!cartBelongsToUser)
                return Unauthorized(new { message = "Cart item does not belong to current user." });
        }

        // Validation stock
        if (dbItem.Product != null && dbItem.Product.StockQuantity < dto.Quantity)
            return BadRequest(new { message = $"Insufficient stock. Available: {dbItem.Product.StockQuantity}" });

        dbItem.Quantity = dto.Quantity;
        _context.CartItems.Update(dbItem);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Xoá một product khỏi shopping cart.
    /// Với guest cart: xoá khỏi cache bằng ID âm
    /// Với user cart: xoá khỏi database bằng ID dương và kiểm tra quyền sở hữu
    /// </summary>
    /// <param name="id">ID của cart item cần xoá (âm cho guest, dương cho user)</param>
    /// <returns>NoContent nếu thành công</returns>
    /// <response code="204">Xoá item thành công</response>
    /// <response code="404">Cart item không tồn tại hoặc không thuộc về user</response>
    [HttpDelete("items/{id}")]
    public async Task<IActionResult> RemoveItem(int id)
    {
        var sessionId = ReadSessionIdFromCookie();
        var userId = GetUserIdFromClaims();

        // Xử lý guest cart items (ID âm)
        if (id < 0)
        {
            if (string.IsNullOrEmpty(sessionId))
                return Unauthorized(new { message = "SessionId required for guest cart." });

            var guestCart = await _cartService.GetGuestCartAsync(sessionId);
            if (guestCart == null)
                return NotFound();

            var itemExists = guestCart.Items.Any(i => i.Id == id);
            if (!itemExists)
                return NotFound();

            // Xoá khỏi cache
            guestCart.Items.RemoveAll(i => i.Id == id);
            await _cartService.SaveGuestCartAsync(sessionId, guestCart);
            return NoContent();
        }

        // Xử lý user cart items (ID dương)
        if (!userId.HasValue)
            return Unauthorized(new { message = "Authentication is required to remove a saved cart item." });

        var dbItem = await _context.CartItems.FirstOrDefaultAsync(ci => ci.Id == id);
        if (dbItem == null)
            return NotFound();

        var cartBelongsToUser = await _context.Carts
            .AnyAsync(c => c.Id == dbItem.CartId && c.UserId == userId);

        if (!cartBelongsToUser)
            return Unauthorized(new { message = "Cart item does not belong to current user." });

        _context.CartItems.Remove(dbItem);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
