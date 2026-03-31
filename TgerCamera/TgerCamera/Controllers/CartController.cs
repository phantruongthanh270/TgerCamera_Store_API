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
/// Handles shopping cart operations including retrieving carts, adding items, updating quantities, and removing items.
/// Supports both guest carts (via SessionId cookie in Distributed Cache) and authenticated user carts (in Database).
/// 
/// Architecture:
/// - Guest Carts: Stored in Distributed Cache (Redis/MemoryCache) with 24-hour TTL, key format: "cart:{sessionId}"
/// - User Carts: Stored in SQL Server database, keyed by UserId
/// - Merge Logic: When authenticated user accesses cart, guest cart automatically merges into user cart with quantity aggregation
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;
    private readonly ICartService _cartService;
    private const string SESSION_COOKIE_NAME = "SessionId";
    private const int SESSION_COOKIE_EXPIRATION_DAYS = 30;

    /// <summary>
    /// Initializes a new instance of the CartController.
    /// </summary>
    public CartController(TgerCameraContext context, IMapper mapper, ICartService cartService)
    {
        _context = context;
        _mapper = mapper;
        _cartService = cartService;
    }

    /// <summary>
    /// Extracts SessionId from request cookies.
    /// </summary>
    private string? ReadSessionIdFromCookie()
    {
        Request.Cookies.TryGetValue(SESSION_COOKIE_NAME, out var sessionId);
        return sessionId;
    }

    /// <summary>
    /// Sets SessionId cookie in response with HttpOnly, Lax SameSite, and 30-day expiration.
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
    /// Deletes SessionId cookie when merging guest cart to user cart.
    /// </summary>
    private void DeleteSessionIdCookie()
    {
        Response.Cookies.Delete(SESSION_COOKIE_NAME);
    }

    /// <summary>
    /// Extracts UserId from JWT claims.
    /// Tries both standard NameIdentifier and JWT Sub claim names.
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
    /// Retrieves the shopping cart for the current session or authenticated user.
    /// 
    /// Flow:
    /// 1. If user is NOT authenticated: returns guest cart from cache (or empty cart)
    /// 2. If user IS authenticated:
    ///    a. Merges guest cart from cache into user's database cart (if guest cart exists)
    ///    b. Deletes SessionId cookie and clears cache after merge
    ///    c. Returns user's database cart
    /// </summary>
    /// <returns>CartDto containing all cart items and metadata</returns>
    /// <response code="200">Returns the user's or guest cart with items</response>
    [HttpGet]
    public async Task<ActionResult<CartDto>> Get()
    {
        var sessionId = ReadSessionIdFromCookie();
        var userId = GetUserIdFromClaims();

        CartDto? cart = null;

        // If user is authenticated: attempt to merge guest cart and return user cart
        if (userId.HasValue)
        {
            // Merge guest cart (if exists in cache) into user's database cart
            if (!string.IsNullOrEmpty(sessionId))
            {
                await _cartService.MergeGuestCartToUserAsync(userId.Value, sessionId);
                DeleteSessionIdCookie();
            }

            // Return user's database cart
            cart = await _cartService.GetUserCartAsync(userId.Value);
            if (cart == null)
            {
                cart = new CartDto { UserId = userId.Value, Items = new List<CartItemDto>() };
            }
        }
        else
        {
            // Guest user: return cart from cache or empty cart
            if (!string.IsNullOrEmpty(sessionId))
            {
                cart = await _cartService.GetGuestCartAsync(sessionId);
            }

            if (cart == null)
            {
                // Create new guest session
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
    /// Adds a product to the shopping cart or increases quantity if already present.
    /// 
    /// Validation:
    /// - Quantity must be positive
    /// - Product must exist and not be deleted
    /// - Stock must be sufficient
    /// - Total quantity after adding cannot exceed available stock
    /// </summary>
    /// <param name="dto">The add to cart request containing product ID and desired quantity</param>
    /// <returns>Updated CartDto with the new item</returns>
    /// <response code="200">Item added/updated successfully</response>
    /// <response code="400">Validation failed (invalid product, insufficient stock, invalid quantity)</response>
    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem([FromBody] CartItemCreateDto dto)
    {
        try
        {
            if (dto.Quantity <= 0)
                return BadRequest(new { message = "Quantity must be greater than 0." });

            var sessionId = ReadSessionIdFromCookie();
            var userId = GetUserIdFromClaims();

            // Add to appropriate storage (cache for guest, database for user)
            var updatedCart = await _cartService.AddItemToCacheOrDbAsync(sessionId, userId, dto.ProductId, dto.Quantity);

            // For guest users: ensure sessionId is set in cookie
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
    /// Updates the quantity of an existing cart item.
    /// For guest carts: Updates item in cache (ID < 0)
    /// For user carts: Updates item in database (ID > 0)
    /// Validates that quantity doesn't exceed product stock.
    /// </summary>
    /// <param name="id">The cart item ID to update (negative for guest, positive for user)</param>
    /// <param name="dto">The update request containing new quantity</param>
    /// <returns>NoContent on success</returns>
    /// <response code="204">Quantity updated successfully</response>
    /// <response code="400">Invalid quantity or exceeds stock</response>
    /// <response code="404">Cart item not found or doesn't belong to user</response>
    [HttpPut("items/{id}")]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] CartItemUpdateDto dto)
    {
        if (dto.Quantity <= 0)
            return BadRequest(new { message = "Quantity must be greater than 0." });

        var sessionId = ReadSessionIdFromCookie();
        var userId = GetUserIdFromClaims();

        // Handle guest cart items (negative ID)
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

            // Update quantity and save to cache
            item.Quantity = dto.Quantity;
            await _cartService.SaveGuestCartAsync(sessionId, guestCart);
            return NoContent();
        }

        // Handle user cart items (positive ID)
        var dbItem = await _context.CartItems
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.Id == id);

        if (dbItem == null)
            return NotFound();

        // If user is authenticated, verify the item belongs to their cart
        if (userId.HasValue)
        {
            var cartBelongsToUser = await _context.Carts
                .AnyAsync(c => c.Id == dbItem.CartId && c.UserId == userId);

            if (!cartBelongsToUser)
                return Unauthorized(new { message = "Cart item does not belong to current user." });
        }

        // Validate stock
        if (dbItem.Product != null && dbItem.Product.StockQuantity < dto.Quantity)
            return BadRequest(new { message = $"Insufficient stock. Available: {dbItem.Product.StockQuantity}" });

        dbItem.Quantity = dto.Quantity;
        _context.CartItems.Update(dbItem);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Removes a product from the shopping cart.
    /// For guest carts: Removes from cache using negative ID
    /// For user carts: Removes from database using positive ID with ownership check
    /// </summary>
    /// <param name="id">The cart item ID to remove (negative for guest, positive for user)</param>
    /// <returns>NoContent on success</returns>
    /// <response code="204">Item removed successfully</response>
    /// <response code="404">Cart item not found or doesn't belong to user</response>
    [HttpDelete("items/{id}")]
    public async Task<IActionResult> RemoveItem(int id)
    {
        var sessionId = ReadSessionIdFromCookie();
        var userId = GetUserIdFromClaims();

        // Handle guest cart items (negative ID)
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

            // Remove from cache
            guestCart.Items.RemoveAll(i => i.Id == id);
            await _cartService.SaveGuestCartAsync(sessionId, guestCart);
            return NoContent();
        }

        // Handle user cart items (positive ID)
        var dbItem = await _context.CartItems.FirstOrDefaultAsync(ci => ci.Id == id);
        if (dbItem == null)
            return NotFound();

        // If user is authenticated, verify the item belongs to their cart
        if (userId.HasValue)
        {
            var cartBelongsToUser = await _context.Carts
                .AnyAsync(c => c.Id == dbItem.CartId && c.UserId == userId);

            if (!cartBelongsToUser)
                return Unauthorized(new { message = "Cart item does not belong to current user." });
        }

        _context.CartItems.Remove(dbItem);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
