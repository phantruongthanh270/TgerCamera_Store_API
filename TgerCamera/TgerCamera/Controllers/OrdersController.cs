using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos.Order;
using TgerCamera.Models;

namespace TgerCamera.Controllers;

/// <summary>
/// Handles order-related operations including checkout, order retrieval, and order status management.
/// Manages the complete ordering workflow from cart to order tracking.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the OrdersController.
    /// </summary>
    /// <param name="context">The database context for accessing order data.</param>
    /// <param name="mapper">AutoMapper instance for DTO mapping.</param>
    public OrdersController(TgerCameraContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <summary>
    /// Processes checkout for authenticated users, converting cart items to an order.
    /// Clears the users cart after successful order creation.
    /// </summary>
    /// <param name="dto">The checkout request containing shipping and payment information.</param>
    /// <returns>Returns the created OrderDto with order ID and total price.</returns>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Checkout(CheckoutDto dto)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(userIdClaim, out var userId)) return Unauthorized();

        // Get user's cart
        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.CartItems.Any()) return BadRequest("Cart is empty.");

        // Create order
        var order = new Order
        {
            UserId = userId,
            Status = "Pending",
            TotalPrice = 0,
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        decimal total = 0;
        foreach (var item in cart.CartItems)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product == null) continue;
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = item.Quantity,
                Price = product.Price
            };
            total += product.Price * item.Quantity;
            _context.OrderItems.Add(orderItem);
        }

        order.TotalPrice = total;
        _context.Orders.Update(order);

        // clear cart
        _context.CartItems.RemoveRange(cart.CartItems);
        _context.Carts.Remove(cart);

        await _context.SaveChangesAsync();

        var result = _mapper.Map<OrderDto>(order);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all orders for the authenticated user.
    /// </summary>
    /// <returns>Returns a list of OrderDto for the user's orders (excluding deleted).</returns>
    [HttpGet("my-orders")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<OrderDto>>> MyOrders()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(userIdClaim, out var userId)) return Unauthorized();

        var orders = await _context.Orders
            .Where(o => o.UserId == userId && (o.IsDeleted == null || o.IsDeleted == false))
            .Include(o => o.OrderItems)
            .ToListAsync();

        return Ok(_mapper.Map<IEnumerable<OrderDto>>(orders));
    }

    /// <summary>
    /// Retrieves all orders in the system. Admin only.
    /// </summary>
    /// <returns>Returns a list of all OrderDto in the system (excluding deleted).</returns>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll()
    {
        var orders = await _context.Orders
            .Where(o => o.IsDeleted == null || o.IsDeleted == false)
            .Include(o => o.OrderItems)
            .ToListAsync();
        return Ok(_mapper.Map<IEnumerable<OrderDto>>(orders));
    }

    /// <summary>
    /// Updates the status of an order. Admin only.
    /// </summary>
    /// <param name="id">The order ID to update.</param>
    /// <param name="status">The new order status (e.g., "Pending", "Processing", "Shipped", "Delivered").</param>
    /// <returns>Returns NoContent on success, or NotFound if order doesn't exist.</returns>
    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();
        order.Status = status;
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
