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
/// Supports both guest carts (via SessionId cookie) and authenticated user carts.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;
    private readonly ICartService _cartService;

    /// <summary>
    /// Initializes a new instance of the CartController.
    /// </summary>
    /// <param name="context">The database context for accessing cart and product data.</param>
    /// <param name="mapper">AutoMapper instance for DTO mapping.</param>
    /// <param name="cartService">Service for cart operations and guest/user cart management.</param>
    public CartController(TgerCameraContext context, IMapper mapper, ICartService cartService)
    {
        _context = context;
        _mapper = mapper;
        _cartService = cartService;
    }

    private string? ReadSessionIdFromCookie()
    {
        if (Request.Cookies.TryGetValue("SessionId", out var sessionId)) return sessionId;
        return null;
    }

    private void SetSessionIdCookie(string sessionId)
    {
        Response.Cookies.Append("SessionId", sessionId, new Microsoft.AspNetCore.Http.CookieOptions
        {
            HttpOnly = true,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = System.DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    /// <summary>
    /// Retrieves the shopping cart for the current session or authenticated user.
    /// If user is authenticated and has a guest cart, automatically merges the guest cart into the user cart.
    /// </summary>
    /// <returns>Returns a CartDto containing all cart items, or creates a new cart if none exists.</returns>
    [HttpGet]
    public async Task<ActionResult<CartDto>> Get()
    {
        var sessionId = ReadSessionIdFromCookie();
        var cart = await _cartService.GetOrCreateCartBySessionAsync(sessionId ?? string.Empty);

        // if user is authenticated, merge guest cart
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                await _cartService.MergeCartAsync(userId, cart.SessionId ?? string.Empty);
            }
        }

        if (string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(cart.SessionId))
        {
            SetSessionIdCookie(cart.SessionId!);
        }

        return Ok(_mapper.Map<CartDto>(cart));
    }

    /// <summary>
    /// Adds a product to the shopping cart or increases quantity if already present.
    /// Creates a new cart if one doesn't exist for the session/user.
    /// </summary>
    /// <param name="dto">The add to cart request containing product ID and desired quantity.</param>
    /// <returns>Returns the updated CartDto on success, or BadRequest if product doesn't exist or validation fails.</returns>
    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem(CartItemCreateDto dto)
    {
        if (dto.Quantity <= 0) return BadRequest("Quantity must be greater than 0.");

        var productExists = await _context.Products.AnyAsync(p => p.Id == dto.ProductId && (p.IsDeleted == null || p.IsDeleted == false));
        if (!productExists) return BadRequest("Product does not exist.");

        var sessionId = ReadSessionIdFromCookie();
        var cart = await _cartService.GetOrCreateCartBySessionAsync(sessionId ?? string.Empty);

        var existing = cart.CartItems.FirstOrDefault(ci => ci.ProductId == dto.ProductId);
        if (existing != null)
        {
            existing.Quantity += dto.Quantity;
            _context.CartItems.Update(existing);
        }
        else
        {
            var item = new CartItem { ProductId = dto.ProductId, Quantity = dto.Quantity, CartId = cart.Id };
            _context.CartItems.Add(item);
            cart.CartItems.Add(item);
        }

        await _context.SaveChangesAsync();

        // Reload cart from database to get the latest items
        cart = await _cartService.GetOrCreateCartBySessionAsync(cart.SessionId ?? string.Empty);

        if (string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(cart.SessionId))
        {
            SetSessionIdCookie(cart.SessionId!);
        }

        return Ok(_mapper.Map<CartDto>(cart));
    }

    /// <summary>
    /// Updates the quantity of an existing cart item.
    /// </summary>
    /// <param name="id">The cart item ID to update.</param>
    /// <param name="dto">The update request containing the new quantity.</param>
    /// <returns>Returns NoContent on success, or NotFound if the cart item doesn't exist.</returns>
    [HttpPut("items/{id}")]
    public async Task<IActionResult> UpdateItem(int id, CartItemUpdateDto dto)
    {
        var item = await _context.CartItems.FindAsync(id);
        if (item == null) return NotFound();
        item.Quantity = dto.Quantity;
        _context.CartItems.Update(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Removes a product from the shopping cart.
    /// </summary>
    /// <param name="id">The cart item ID to remove.</param>
    /// <returns>Returns NoContent on success, or NotFound if the cart item doesn't exist.</returns>
    [HttpDelete("items/{id}")]
    public async Task<IActionResult> RemoveItem(int id)
    {
        var item = await _context.CartItems.FindAsync(id);
        if (item == null) return NotFound();
        _context.CartItems.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
